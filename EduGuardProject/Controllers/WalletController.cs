using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Filters;
using EduGuardProject.Models;
using EduGuardProject.Services.IServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace EduGuardProject.Controllers
{
    [Route("api/wallets")]
    [ApiController]
    public class WalletController : ControllerBase
    {
        private readonly IWalletService _walletService;
        private readonly IUserService _userService; // 🌟 Bổ sung Service này để check danh tính người đang gọi API

        public WalletController(IWalletService walletService, IUserService userService)
        {
            _walletService = walletService;
            _userService = userService;
        }

        [HttpGet("institution/{institutionId}")]
        [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin)]
        public async Task<IActionResult> GetWalletByInstitutionId(Guid institutionId)
        {
            try
            {
                // ================= BẢO MẬT: CHỐNG XEM TRỘM VÍ TRƯỜNG KHÁC =================
                if (HttpContext.Items["UserId"] is not Guid currentUserId)
                    return Unauthorized(ApiResponse<object>.OnFail("User session is invalid."));

                var currentUser = await _userService.GetUserByIdAsync(currentUserId);

                if (currentUser == null)
                    return Unauthorized(ApiResponse<object>.OnFail("User session is invalid."));

                // Nếu người gọi là SchoolAdmin, BẮT BUỘC ID truyền lên phải trùng với ID trường của nó!
                if (currentUser.Role == AppRole.SchoolAdmin && currentUser.InstitutionId != institutionId)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.OnFail("Forbidden: You can only view your own institution's wallet."));
                }
                // (Còn nếu là SuperAdmin thì đoạn if trên sẽ bị bỏ qua, SuperAdmin được quyền xem mọi ví)
                // =========================================================================

                var wallet = await _walletService.GetWalletByInstitutionIdAsync(institutionId);
                if (wallet == null)
                {
                    return NotFound(ApiResponse<object>.OnFail("Wallet for this institution not found."));
                }
                return Ok(ApiResponse<WalletResponseDto>.OnSuccess(wallet, "Wallet information retrieved successfully."));
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.OnFail($"System error: {ex.Message}"));
            }
        }

        [HttpGet("{walletId}/transactions")]
        [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin)]
        public async Task<IActionResult> GetTransactions(Guid walletId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                if (page < 1 || pageSize < 1)
                {
                    return BadRequest(ApiResponse<object>.OnFail("Page and PageSize must be greater than 0."));
                }

                var result = await _walletService.GetTransactionHistoryAsync(walletId, page, pageSize);
                var response = ApiPagedResponse<TransactionResponseDto>.OnPagedSuccess(
                    result.Data, page, pageSize, result.TotalItems, "Transaction history retrieved successfully."
                );

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.OnFail($"System error: {ex.Message}"));
            }
        }

        [HttpPost("top-up")]
        [SupabaseAuthorize(AppRole.SchoolAdmin)] // Chỉ School Admin nạp tiền
        public async Task<IActionResult> TopUpWallet([FromBody] TopUpRequestDto dto)
        {
            try
            {
                if (HttpContext.Items["UserId"] is not Guid currentUserId)
                    return Unauthorized(ApiResponse<object>.OnFail("User session is invalid."));

                var currentUser = await _userService.GetUserByIdAsync(currentUserId);

                if (currentUser == null)
                    return Unauthorized(ApiResponse<object>.OnFail("User session is invalid."));

                // 1. Chặn Admin vô gia cư (Chưa được gán vào trường)
                if (currentUser.InstitutionId == null || currentUser.InstitutionId == Guid.Empty)
                {
                    return BadRequest(ApiResponse<object>.OnFail("Your account is not assigned to any institution. Deposit denied."));
                }

                // 2. Chống giả mạo: ÉP CỨNG InstitutionId của DTO thành InstitutionId của chính thằng đang đăng nhập
                // (Không thèm quan tâm Frontend gửi cái InstitutionId nào lên, ta cứ lấy của thằng đang đăng nhập cho chắc củ!)
                dto.InstitutionId = currentUser.InstitutionId.Value;

                var paymentUrl = await _walletService.ProcessTopUpAsync(dto, HttpContext);
                return Ok(ApiResponse<string>.OnSuccess(paymentUrl, "VNPay payment link created successfully. Please follow the link to complete payment."));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.OnFail(ex.Message));
            }
        }

        // VNPay calls this callback without application authentication.
        [HttpGet("vnpay-return")]
        public async Task<IActionResult> VnPayReturn()
        {
            try
            {
                var result = await _walletService.ProcessVnPayReturnAsync(Request.Query);
                var feUrl = $"https://edu-guard-system-fe.vercel.app/school/wallet-result" +
                            $"?success={result.Success}" +
                            $"&message={Uri.EscapeDataString(result.Message)}" +
                            $"&amount={result.Amount}" +
                            $"&txnRef={result.TxnRef}";
                return Redirect(feUrl);
            }
            catch (Exception ex)
            {
                var feUrl = $"https://edu-guard-system-fe.vercel.app/school/wallet-result" +
                            $"?success=false&message={Uri.EscapeDataString($"System error: {ex.Message}")}";
                return Redirect(feUrl);
            }
        }
    }
}
