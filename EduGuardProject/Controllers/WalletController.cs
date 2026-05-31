using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Filters;
using EduGuardProject.Models;
using EduGuardProject.Services.IServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EduGuardProject.Controllers
{
    [Route("api/wallets")]
    [ApiController]
    public class WalletController : ControllerBase
    {
        private readonly IWalletService _walletService;

        public WalletController(IWalletService walletService)
        {
            _walletService = walletService;
        }

        [HttpGet("institution/{institutionId}")]
        [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin)] 
        public async Task<IActionResult> GetWalletByInstitutionId(Guid institutionId)
        {
            try
            {
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
        [SupabaseAuthorize(AppRole.SchoolAdmin)] // Thường là School Admin nạp tiền
        public async Task<IActionResult> TopUpWallet([FromBody] TopUpRequestDto dto)
        {
            try
            {
                var paymentUrl = await _walletService.ProcessTopUpAsync(dto, HttpContext);
                return Ok(ApiResponse<string>.OnSuccess(paymentUrl, "VNPay payment link created successfully. Please follow the link to complete payment."));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.OnFail(ex.Message));
            }
        }

        // ⚠️ KHÔNG ĐỂ [SupabaseAuthorize] Ở ĐÂY VÌ VNPAY SẼ GỌI VÀO HÀM NÀY
        [HttpGet("vnpay-return")]
        public async Task<IActionResult> VnPayReturn()
        {
            try
            {
                var isSuccess = await _walletService.ProcessVnPayReturnAsync(Request.Query);
                if (isSuccess)
                {
                    return Ok(ApiResponse<object>.OnSuccess(null, "VNPay payment successful, wallet topped up!"));
                }
                return BadRequest(ApiResponse<object>.OnFail("Payment failed or invalid signature."));
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.OnFail($"System error: {ex.Message}"));
            }
        }
    }
}
