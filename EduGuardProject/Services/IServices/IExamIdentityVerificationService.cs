using EduGuardProject.Models;
using Microsoft.AspNetCore.Http;

namespace EduGuardProject.Services.IServices
{
    public interface IExamIdentityVerificationService
    {
        Task<IdentityVerificationResult> VerifyAsync(
            Guid participationId, Guid studentId, IFormFile liveCapture,
            CancellationToken cancellationToken = default);

        Task<bool> ManualApproveIdentityAsync(
            Guid participationId, CancellationToken cancellationToken = default);

        // Kiểm tra giám thị đã duyệt tay chưa (AI verify không tính), không cần gửi ảnh.
        Task<bool> IsIdentityVerifiedAsync(
            Guid participationId, CancellationToken cancellationToken = default);
    }

    public class IdentityVerificationResult
    {
        public bool IsMatch { get; set; }
        public double Distance { get; set; }
        public string? SnapshotPath { get; set; }
    }
}