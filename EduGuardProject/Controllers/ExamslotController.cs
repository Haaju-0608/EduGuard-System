using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Models;
using EduGuardProject.Services.IServices;
using Microsoft.AspNetCore.Mvc;

namespace EduGuardProject.Controllers
{
    [Route("api/exam-slots")]
    [ApiController]
    public class ExamslotController : AcademicApiControllerBase
    {
        private readonly IExamSlotServices _service;

        public ExamslotController(IExamSlotServices service) => _service = service;

        [HttpGet]
        public async Task<IActionResult> GetAll(
           [FromQuery] string? search,
            [FromQuery] string? sort,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            if (!ValidatePaging(page, pageSize)) return BadPagedRequest("Page and pageSize must be greater than 0.");
            try
            {
                var (items, total) = await _service.GetAllExamSlotsAsync(search, sort, page, pageSize);
                var response = ApiPagedResponse<ExamSlot>.OnPagedSuccess(items, page, pageSize, total, "Exam slot retrieved successfully.");
                return Ok(response);
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        [HttpGet("{examId:guid}")]
        public async Task<IActionResult> GetById(Guid examId)
        {
            try
            {
                var item = await _service.GetByIdAsync(examId);
                if (item == null) return NotFound(ApiResponse<object>.OnFail("Exam slot not found."));
                return OkSingle(item, "Exam slot retrieved successfully.");
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateExamSlotDto dto)
        {
            try
            {
                var result = await _service.CreateAsync(dto);
                return CreatedSingle(result, "Exam slot created successfully.");
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        [HttpPut("{examId:guid}")]
        public async Task<IActionResult> Update(Guid examId, [FromBody] UpdateExamSlotDto dto)
        {
            try
            {
                var success = await _service.UpdateAsync(examId, dto);
                if (!success) return NotFound(ApiResponse<object>.OnFail("Exam slot not found."));
                return Ok(ApiResponse<object>.OnSuccess(null!, "Exam slot updated successfully."));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        [HttpDelete("{examId:guid}")]
        public async Task<IActionResult> Delete(Guid examId)
        {
            try
            {
                var success = await _service.DeleteAsync(examId);
                if (!success) return NotFound(ApiResponse<object>.OnFail("Exam slot not found."));
                return Ok(ApiResponse<object>.OnSuccess(null!, "Exam slot deleted successfully."));
            }
            catch (Exception ex) { return HandleException(ex); }
        }
    }
}
