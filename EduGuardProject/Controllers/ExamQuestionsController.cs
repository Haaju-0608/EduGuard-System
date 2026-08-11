using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Filters;
using EduGuardProject.Models;
using EduGuardProject.Services.IServices;
using Microsoft.AspNetCore.Mvc;

namespace EduGuardProject.Controllers;

[Route("api/exam-questions")]
[ApiController]
[SupabaseAuthorize]
public class ExamQuestionsController : AcademicApiControllerBase
{
    private readonly IExamQuestionService _service;

    public ExamQuestionsController(IExamQuestionService service) => _service = service;

    // Roles: all authenticated roles; Student sees only participated exams and IsCorrect is null. Returns: paged ExamQuestionResponseDto list.
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? sort,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] Guid? examSlotId = null,
        [FromQuery] string? fields = null)
    {
        if (!ValidatePaging(page, pageSize)) return BadPagedRequest("Page and pageSize must be greater than 0.");

        try
        {
            var (items, total) = await _service.GetAllAsync(search, sort, page, pageSize, examSlotId);
            return OkPaged(items, page, pageSize, total, "Exam questions retrieved successfully.", fields);
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    // Roles: all authenticated roles with scoped access; Student gets IsCorrect as null. Returns: one ExamQuestionResponseDto.
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, [FromQuery] string? fields = null)
    {
        try
        {
            var item = await _service.GetByIdAsync(id);
            if (item == null) return NotFound(ApiResponse<object>.OnFail("Exam question not found."));
            return OkSingle(item, "Exam question retrieved successfully.", fields);
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    // Roles: Lecturer, SchoolAdmin, SuperAdmin. Returns: created ExamQuestionResponseDto including IsCorrect.
    [HttpPost]
    [SupabaseAuthorize(AppRole.Lecturer, AppRole.SchoolAdmin, AppRole.SuperAdmin)]
    public async Task<IActionResult> Create([FromBody] CreateExamQuestionDto dto, [FromQuery] string? fields = null)
    {
        try
        {
            var result = await _service.CreateAsync(dto);
            return CreatedSingle(result, "Exam question created successfully.", fields);
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    // Roles: Lecturer, SchoolAdmin, SuperAdmin. Returns: updated ExamQuestionResponseDto including IsCorrect.
    [HttpPut("{id:guid}")]
    [SupabaseAuthorize(AppRole.Lecturer, AppRole.SchoolAdmin, AppRole.SuperAdmin)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateExamQuestionDto dto, [FromQuery] string? fields = null)
    {
        try
        {
            var result = await _service.UpdateAsync(id, dto);
            if (result == null) return NotFound(ApiResponse<object>.OnFail("Exam question not found."));
            return OkSingle(result, "Exam question updated successfully.", fields);
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    // Roles: Lecturer, SchoolAdmin, SuperAdmin. Returns: success message only.
    [HttpDelete("{id:guid}")]
    [SupabaseAuthorize(AppRole.Lecturer, AppRole.SchoolAdmin, AppRole.SuperAdmin)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var success = await _service.DeleteAsync(id);
            if (!success) return NotFound(ApiResponse<object>.OnFail("Exam question not found."));
            return Ok(ApiResponse<object>.OnSuccess(null!, "Exam question deleted successfully."));
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    // Roles: Lecturer, SchoolAdmin, SuperAdmin. Returns: created QuestionOptionResponseDto including IsCorrect.
    [HttpPost("{questionId:guid}/options")]
    [SupabaseAuthorize(AppRole.Lecturer, AppRole.SchoolAdmin, AppRole.SuperAdmin)]
    public async Task<IActionResult> CreateOption(Guid questionId, [FromBody] CreateQuestionOptionDto dto)
    {
        try
        {
            var result = await _service.CreateOptionAsync(questionId, dto);
            return CreatedSingle(result, "Question option created successfully.");
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    // Roles: Lecturer, SchoolAdmin, SuperAdmin. Returns: updated QuestionOptionResponseDto including IsCorrect.
    [HttpPut("options/{optionId:guid}")]
    [SupabaseAuthorize(AppRole.Lecturer, AppRole.SchoolAdmin, AppRole.SuperAdmin)]
    public async Task<IActionResult> UpdateOption(Guid optionId, [FromBody] UpdateQuestionOptionDto dto)
    {
        try
        {
            var result = await _service.UpdateOptionAsync(optionId, dto);
            if (result == null) return NotFound(ApiResponse<object>.OnFail("Question option not found."));
            return OkSingle(result, "Question option updated successfully.");
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    // Roles: Lecturer, SchoolAdmin, SuperAdmin. Returns: success message only.
    [HttpDelete("options/{optionId:guid}")]
    [SupabaseAuthorize(AppRole.Lecturer, AppRole.SchoolAdmin, AppRole.SuperAdmin)]
    public async Task<IActionResult> DeleteOption(Guid optionId)
    {
        try
        {
            var success = await _service.DeleteOptionAsync(optionId);
            if (!success) return NotFound(ApiResponse<object>.OnFail("Question option not found."));
            return Ok(ApiResponse<object>.OnSuccess(null!, "Question option deleted successfully."));
        }
        catch (Exception ex) { return HandleException(ex); }
    }
}
