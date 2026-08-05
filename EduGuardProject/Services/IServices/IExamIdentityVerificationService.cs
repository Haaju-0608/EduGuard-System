namespace EduGuardProject.Services.IServices
{
    public interface IExamIdentityVerificationService
    {
        Task<IdentityVerificationResult> VerifyAsync(
            Guid participationId, Guid studentId, IFormFile liveCapture,
            CancellationToken cancellationToken = default);
    }

    public class IdentityVerificationResult
    {
        public bool IsMatch { get; set; }
        public double Distance { get; set; }
        public string? SnapshotPath { get; set; }
    }
}
