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

        // Chỉ trả true khi đã được duyệt tay; xác thực bằng AI vẫn cần ảnh mỗi lần vào thi.
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
