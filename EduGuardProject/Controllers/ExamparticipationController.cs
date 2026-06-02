using EduGuardProject.Controllers;
using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Filters;
using EduGuardProject.Models;
using EduGuardProject.Repositories.IRepositories;
using EduGuardProject.Services.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduGuardProject.Controllers
{
    [Route("api/exam-participations")]
    [ApiController]
    public class ExamParticipationController : AcademicApiControllerBase
    {
        private readonly IExamParticipationService _service;
        public ExamParticipationController(IExamParticipationService service) => _service = service;


        [HttpGet]
        [SupabaseAuthorize(AppRole.SuperAdmin)]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? search,
            [FromQuery] string? sort,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            if (!ValidatePaging(page, pageSize)) return BadPagedRequest("Page and pageSize must be greater than 0.");
            try
            {
                var (items, total) = await _service.GetAllExamparticipationsAsync(search, sort, page, pageSize);
                return OkPaged(items, page, pageSize, total, "Exam participations retrieved successfully.",null);
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var item = await _service.GetByIdAsync(id);
                if (item == null) return NotFound(ApiResponse<object>.OnFail("Exam participation not found."));
                return OkSingle(item, "Exam participation retrieved successfully.");
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        [HttpPost]
        [SupabaseAuthorize(AppRole.SuperAdmin)]
        public async Task<IActionResult> Create([FromBody] CreateExamParticipationDto dto)
        {
            try
            {
                var result = await _service.CreateAsync(dto);
                return CreatedSingle(result, "Exam participation created successfully.");
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        [HttpPut("{examSlotId:guid}")]
        public async Task<IActionResult> Update(Guid examSlotId, [FromBody] UpdateExamParticipationDto dto)
        {
            try
            {
                var success = await _service.UpdateAsync(examSlotId, dto);
                if (!success) return NotFound(ApiResponse<object>.OnFail("Exam participation not found."));
                return Ok(ApiResponse<object>.OnSuccess(null!, "Exam participation updated successfully."));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        [HttpDelete("{examSlotId:guid}")]
        public async Task<IActionResult> Delete(Guid examSlotId)
        {
            try
            {
                var success = await _service.DeleteAsync(examSlotId);
                if (!success) return NotFound(ApiResponse<object>.OnFail("Exam participation not found."));
                return Ok(ApiResponse<object>.OnSuccess(null!, "Exam participation deleted successfully."));
            }
            catch (Exception ex) { return HandleException(ex); }
        }
    }
}   
