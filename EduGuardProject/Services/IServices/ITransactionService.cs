using EduGuardProject.DTOs.Response;

namespace EduGuardProject.Services.IServices
{
    public interface ITransactionService
    {
        // 1. Lấy lịch sử giao dịch (Có phân trang)
        Task<(IEnumerable<TransactionResponseDto> Data, int TotalItems)> GetTransactionsByWalletAsync(Guid walletId, int page, int pageSize);

        // 2. Trừ tiền phí Điểm danh (Gọi nội bộ từ service khác)
        Task<TransactionResponseDto> DeductAttendanceFeeAsync(Guid walletId, Guid attendanceSessionId, int studentCount);

        // 3. Trừ tiền phí Giám thị (Gọi nội bộ từ service khác)
        Task<TransactionResponseDto> DeductProctoringFeeAsync(Guid walletId, Guid examParticipationId, int hours);
    }
}
