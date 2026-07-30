using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;

namespace EduGuardProject.Services.IServices
{
    public interface IWalletService
    {
        Task<WalletResponseDto?> GetWalletByInstitutionIdAsync(Guid institutionId);
        Task<(IEnumerable<TransactionResponseDto> Data, int TotalItems)> GetTransactionHistoryAsync(Guid walletId, int page, int pageSize);

        // Hàm này xử lý nạp tiền: Vừa tạo Transaction, vừa cộng Balance vào Wallet
        Task<TransactionResponseDto> ProcessTopUpAsync(TopUpRequestDto dto);

        //VNPAY:
        Task<string> ProcessTopUpAsync(TopUpRequestDto dto, HttpContext httpContext);
        Task<VnPayReturnResultDto> ProcessVnPayReturnAsync(IQueryCollection collections); 

    }
}
