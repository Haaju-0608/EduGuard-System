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
            var biometric = await _context.BiometricData
                .Where(b => b.UserId == studentId && b.IsActive)
                .OrderByDescending(b => b.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (biometric?.FaceVector is null)
                throw new InvalidOperationException("Student has no active biometric profile registered.");

            float[] liveVector;
            using (var liveStream = liveCapture.OpenReadStream())
            {
                liveVector = await _aiService.ExtractSingleFaceVectorAsync(liveStream, liveCapture.FileName);
            }

            var distance = ComputeEuclideanDistance(biometric.FaceVector.ToArray(), liveVector);
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
