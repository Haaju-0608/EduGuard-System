using EduGuardProject.DTOs.Request;
using EduGuardProject.Services.IServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EduGuardProject.Controllers
{
    [Route("api/transactions")]
    [ApiController]
    public class TransactionController : ControllerBase
    {
        private readonly ITransactionService _transactionService;

        public TransactionController(ITransactionService transactionService)
        {
            _transactionService = transactionService;
        }

        // ================= 1. API LẤY LỊCH SỬ GIAO DỊCH =================
        [HttpGet("wallet/{walletId}")]
        public async Task<IActionResult> GetTransactions(Guid walletId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                if (page < 1 || pageSize < 1)
                    return BadRequest(new { success = false, message = "Page và PageSize phải lớn hơn 0.", errors = "Invalid pagination parameters" });

                var result = await _transactionService.GetTransactionsByWalletAsync(walletId, page, pageSize);
                int totalPages = (int)Math.Ceiling((double)result.TotalItems / pageSize);

                return Ok(new
                {
                    success = true,
                    message = "Lấy lịch sử giao dịch thành công.",
                    data = result.Data,
                    pagination = new
                    {
                        page = page,
                        pageSize = pageSize,
                        totalItems = result.TotalItems,
                        totalPages = totalPages
                    },
                    errors = (object)null
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = ex.Message, errors = ex.Message });
            }
        }

        // ================= 2. API TRỪ TIỀN ĐIỂM DANH (DÀNH CHO TEAM AI GỌI) =================
        [HttpPost("deduct-attendance")]
        public async Task<IActionResult> DeductAttendanceFee([FromBody] DeductAttendanceRequestDto request)
        {
            try
            {
                var result = await _transactionService.DeductAttendanceFeeAsync(
                    request.WalletId,
                    request.AttendanceSessionId,
                    request.StudentCount
                );

                return Ok(new
                {
                    success = true,
                    message = "Đã trừ phí điểm danh tự động thành công.",
                    data = result,
                    errors = (object)null
                });
            }
            catch (Exception ex)
            {
                // Văng lỗi HTTP 400 nếu ví hết tiền, chưa cài giá, hoặc đã bị trừ tiền rồi (chống double-billing)
                return BadRequest(new { success = false, message = ex.Message, errors = ex.Message });
            }
        }

        // ================= 3. API TRỪ TIỀN GIÁM THỊ (DÀNH CHO TEAM AI GỌI) =================
        [HttpPost("deduct-proctoring")]
        public async Task<IActionResult> DeductProctoringFee([FromBody] DeductProctoringRequestDto request)
        {
            try
            {
                var result = await _transactionService.DeductProctoringFeeAsync(
                    request.WalletId,
                    request.ExamParticipationId,
                    request.Hours
                );

                return Ok(new
                {
                    success = true,
                    message = "Đã trừ phí giám thị tự động thành công.",
                    data = result,
                    errors = (object)null
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message, errors = ex.Message });
            }
        }
    }
}