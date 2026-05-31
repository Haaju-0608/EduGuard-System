using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Filters;
using EduGuardProject.Models;
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

        [HttpGet("wallet/{walletId}")]
        [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin)]
        public async Task<IActionResult> GetTransactions(Guid walletId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                if (page < 1 || pageSize < 1)
                    return BadRequest(ApiResponse<object>.OnFail("Page and PageSize must be greater than 0."));

                var result = await _transactionService.GetTransactionsByWalletAsync(walletId, page, pageSize);
                return Ok(ApiPagedResponse<TransactionResponseDto>.OnPagedSuccess(result.Data, page, pageSize, result.TotalItems, "Transaction history retrieved successfully."));
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.OnFail($"System error: {ex.Message}"));
            }
        }

        [HttpPost("deduct-attendance")]
        [SupabaseAuthorize(AppRole.SuperAdmin)] // Team AI sẽ nhét Token SuperAdmin vào để gọi hàm này
        public async Task<IActionResult> DeductAttendanceFee([FromBody] DeductAttendanceRequestDto request)
        {
            try
            {
                var result = await _transactionService.DeductAttendanceFeeAsync(request.WalletId, request.AttendanceSessionId, request.StudentCount);
                return Ok(ApiResponse<object>.OnSuccess(result, "Attendance fee deducted successfully."));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.OnFail(ex.Message));
            }
        }

        [HttpPost("deduct-proctoring")]
        [SupabaseAuthorize(AppRole.SuperAdmin)] // Tương tự
        public async Task<IActionResult> DeductProctoringFee([FromBody] DeductProctoringRequestDto request)
        {
            try
            {
                var result = await _transactionService.DeductProctoringFeeAsync(request.WalletId, request.ExamParticipationId, request.Hours);
                return Ok(ApiResponse<object>.OnSuccess(result, "Proctoring fee deducted successfully."));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.OnFail(ex.Message));
            }
        }
    }
}