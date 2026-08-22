using EduGuardProject.Helpers;
using EduGuardProject.Models;
using EduGuardProject.Services.IServices;
using Microsoft.EntityFrameworkCore;

namespace EduGuardProject.Services
{
    public class ExamIdentityVerificationService : IExamIdentityVerificationService
    {
        private readonly AppDbContext _context;
        private readonly IAiServiceClient _aiService;
        private readonly IStorageService _storage;
        private readonly ICurrentUserService _currentUser; 
        private const double IDENTITY_MATCH_TOLERANCE = 0.45;

        public ExamIdentityVerificationService(
            AppDbContext context, IAiServiceClient aiService, IStorageService storage,
            ICurrentUserService currentUser) 
        {
            _context = context;
            _aiService = aiService;
            _storage = storage;
            _currentUser = currentUser; 
        }

        public async Task<IdentityVerificationResult> VerifyAsync(
            Guid participationId, Guid studentId, IFormFile liveCapture,
            CancellationToken cancellationToken = default)
        {
            var studentInstitutionId = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == studentId)
                .Select(u => u.InstitutionId)
                .FirstOrDefaultAsync(cancellationToken);
            await SubscriptionGuard.EnsureInstitutionActiveAsync(_context, studentInstitutionId);

            var biometrics = await _context.BiometricData
                .Where(b => b.UserId == studentId && b.IsActive && b.FaceVector != null)
                .ToListAsync(cancellationToken);
            if (biometrics.Count == 0)
                throw new InvalidOperationException("Student has no active biometric profile registered.");

            float[] liveVector;
            using (var liveStream = liveCapture.OpenReadStream())
            {
                liveVector = await _aiService.ExtractSingleFaceVectorAsync(liveStream, liveCapture.FileName);
            }

            var distance = biometrics
                .Select(b => ComputeEuclideanDistance(b.FaceVector!.ToArray(), liveVector))
                .Min();
            var isMatch = distance <= IDENTITY_MATCH_TOLERANCE;

            string? snapshotPath = null;
            try
            {
                var uploadResult = await _storage.UploadExamIdentityAsync(liveCapture, participationId, cancellationToken);
                snapshotPath = uploadResult.Path;
            }
            catch { /* upload evidence lỗi không chặn luồng verify */ }

            if (isMatch)
            {
                var participation = await _context.ExamParticipations.FindAsync(new object[] { participationId }, cancellationToken);
                if (participation is not null)
                {
                    if (snapshotPath is not null)
                        participation.IdentitySnapshotPath = snapshotPath;
                    participation.IdentityVerifiedAt = DateTime.UtcNow;
                    participation.IdentityVerifiedBy = null; // null = AI tự động xác thực
                    await _context.SaveChangesAsync(cancellationToken);
                }
            }

            return new IdentityVerificationResult { IsMatch = isMatch, Distance = distance, SnapshotPath = snapshotPath };
        }

        // MỚI: giám thị duyệt tay — chỉ giám thị được phân công ca thi này (ProctorId),
        // giáo viên phụ trách lớp, SchoolAdmin cùng trường, hoặc SuperAdmin mới gọi được.
        public async Task<bool> ManualApproveIdentityAsync(
            Guid participationId, CancellationToken cancellationToken = default)
        {
            var participation = await _context.ExamParticipations
                .Include(p => p.ExamSlot).ThenInclude(e => e.Class)
                .FirstOrDefaultAsync(p => p.Id == participationId, cancellationToken)
                ?? throw new InvalidOperationException("Exam participation not found.");

            var staffUser = await EnsureProctorOrStaffAccessAsync(participation);

            participation.IdentityVerifiedAt = DateTime.UtcNow;
            participation.IdentityVerifiedBy = staffUser.Id;
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        private async Task<User> EnsureProctorOrStaffAccessAsync(ExamParticipation participation)
        {
            var user = await _currentUser.GetRequiredUserAsync();
            if (user.Role == AppRole.SuperAdmin) return user;

            var cls = participation.ExamSlot.Class;
            if (user.InstitutionId != cls.InstitutionId)
                throw new UnauthorizedAccessException("Access denied.");

            var isClassLecturer = user.Role == AppRole.Lecturer && cls.LecturerId == user.Id;
            var isExamProctor = user.Role == AppRole.Lecturer && participation.ExamSlot.ProctorId == user.Id;
            var isSchoolAdmin = user.Role == AppRole.SchoolAdmin;

            if (!isClassLecturer && !isExamProctor && !isSchoolAdmin)
                throw new UnauthorizedAccessException(
                    "Only the assigned proctor, class lecturer, or school admin can manually approve identity.");

            return user;
        }

        private static double ComputeEuclideanDistance(float[] a, float[] b)
        {
            if (a.Length != b.Length)
                throw new InvalidOperationException($"Vector dimension mismatch: stored={a.Length}, live={b.Length}.");
            double sum = 0;
            for (int i = 0; i < a.Length; i++) { var diff = a[i] - b[i]; sum += diff * diff; }
            return Math.Sqrt(sum);
        }

        // MỚI: kiểm tra nhanh participation đã được verify chưa (không quan tâm bằng AI hay tay).
        public async Task<bool> IsIdentityVerifiedAsync(
            Guid participationId, CancellationToken cancellationToken = default)
        {
            var verifiedAt = await _context.ExamParticipations
                .AsNoTracking()
                .Where(p => p.Id == participationId)
                .Select(p => p.IdentityVerifiedAt)
                .FirstOrDefaultAsync(cancellationToken);
            return verifiedAt.HasValue;
        }
    }
}