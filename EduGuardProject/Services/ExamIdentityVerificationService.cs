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
        private const double IDENTITY_MATCH_TOLERANCE = 0.45;

        public ExamIdentityVerificationService(AppDbContext context, IAiServiceClient aiService, IStorageService storage)
        {
            _context = context;
            _aiService = aiService;
            _storage = storage;
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

            // So với cả 3 vector (front/left/right), lấy khoảng cách nhỏ nhất — vẫn giữ nguyên
            // ngưỡng 0.45 nên không đổi độ nghiêm ngặt bảo mật, chỉ đổi cách so sánh.
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

            if (isMatch && snapshotPath is not null)
            {
                var participation = await _context.ExamParticipations.FindAsync(new object[] { participationId }, cancellationToken);
                if (participation is not null)
                {
                    participation.IdentitySnapshotPath = snapshotPath;
                    await _context.SaveChangesAsync(cancellationToken);
                }
            }

            return new IdentityVerificationResult { IsMatch = isMatch, Distance = distance, SnapshotPath = snapshotPath };
        }

        private static double ComputeEuclideanDistance(float[] a, float[] b)
        {
            if (a.Length != b.Length)
                throw new InvalidOperationException($"Vector dimension mismatch: stored={a.Length}, live={b.Length}.");
            double sum = 0;
            for (int i = 0; i < a.Length; i++) { var diff = a[i] - b[i]; sum += diff * diff; }
            return Math.Sqrt(sum);
        }
    }
}
