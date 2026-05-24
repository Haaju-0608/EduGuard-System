using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
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

        // 1. LẤY THÔNG TIN VÍ CỦA TRƯỜNG HỌC
        [HttpGet("institution/{institutionId}")]
        public async Task<IActionResult> GetWalletByInstitutionId(Guid institutionId)
        {
            try
            {
                var wallet = await _walletService.GetWalletByInstitutionIdAsync(institutionId);

                if (wallet == null)
                {
                    return NotFound(ApiResponse<object>.OnFail("Không tìm thấy ví cho trường học này.")); // HTTP 404 [cite: 88, 89]
                }

                // 🎯 Sử dụng OnSuccess trả về dữ liệu chuẩn
                return Ok(ApiResponse<WalletResponseDto>.OnSuccess(wallet, "Lấy thông tin ví thành công.")); // HTTP 200 [cite: 83]
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.OnFail(ex.Message)); // HTTP 500 [cite: 90, 91]
            }
        }

        // 2. LẤY LỊCH SỬ GIAO DỊCH CỦA VÍ (CÓ PHÂN TRANG)
        [HttpGet("{walletId}/transactions")]
        public async Task<IActionResult> GetTransactions(Guid walletId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                // Kiểm tra input phân trang
                if (page < 1 || pageSize < 1)
                {
                    return BadRequest(ApiResponse<object>.OnFail("Page và PageSize phải lớn hơn 0."));
                }

                var result = await _walletService.GetTransactionHistoryAsync(walletId, page, pageSize);

                // 🎯 Sử dụng OnPagedSuccess để trả về danh sách kèm theo Metadata Pagination
                var response = ApiPagedResponse<TransactionResponseDto>.OnPagedSuccess(
                    result.Data,
                    page,
                    pageSize,
                    result.TotalItems,
                    "Lấy lịch sử giao dịch thành công."
                );

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.OnFail(ex.Message));
            }
        }

        // 3. NẠP TIỀN VÀO VÍ - BÂY GIỜ TRẢ VỀ LINK VNPAY
        [HttpPost("top-up")]
        public async Task<IActionResult> TopUpWallet([FromBody] TopUpRequestDto dto)
        {
            try
            {
                // Truyền thêm HttpContext vào Service
                var paymentUrl = await _walletService.ProcessTopUpAsync(dto, HttpContext);

                // Trả ra link VNPay bọc trong ApiResponse chuẩn chỉnh
                return Ok(ApiResponse<string>.OnSuccess(paymentUrl, "Tạo link thanh toán VNPay thành công. Vui lòng truy cập đường dẫn để thanh toán."));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.OnFail(ex.Message));
            }
        }

        // 4. ENDPOINT HỨNG DỮ LIỆU TRẢ VỀ TỪ VNPAY (RETURN URL)
        [HttpGet("vnpay-return")]
        public async Task<IActionResult> VnPayReturn()
        {
            try
            {
                var isSuccess = await _walletService.ProcessVnPayReturnAsync(Request.Query);

                if (isSuccess)
                {
                    return Ok(ApiResponse<object>.OnSuccess(null, "Thanh toán qua VNPay thành công, ví đã được cộng tiền!"));
                }

                return BadRequest(ApiResponse<object>.OnFail("Thanh toán thất bại hoặc chữ ký không hợp lệ."));
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.OnFail(ex.Message));
            }
        }
    }
}
