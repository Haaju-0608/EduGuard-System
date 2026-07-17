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
        private readonly IExamWorkflowService _workflowService;

        public ExamParticipationController(IExamParticipationService service, IExamWorkflowService workflowService)
        {
            _service = service;
            _workflowService = workflowService;
        }


        // Get All Exam Participations
        // Truyền dữ liệu: query search, sort, page, pageSize.
        // Điều kiện: SuperAdmin xem tất cả; SchoolAdmin cùng institution; Lecturer đúng class; Student chỉ xem participation của chính mình; page và pageSize phải lớn hơn 0.
        [HttpGet]
        [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin, AppRole.Lecturer, AppRole.Student)]
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
                var response = ApiPagedResponse<ExamParticipationResponseDto>.OnPagedSuccess(items, page, pageSize, total, "Exampartipation retrieved successfully.");
                return Ok(response);

            }
            catch (Exception ex) { return HandleException(ex); }
        }

        // Get Exam Participations By Exam Slot
        // Roles: SuperAdmin all; SchoolAdmin same institution; Lecturer own class; Student own participation. Returns: paged ExamParticipationResponseDto list for one examSlotId.
        [HttpGet("exam-slots/{examSlotId:guid}")]
        [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin, AppRole.Lecturer, AppRole.Student)]
        public async Task<IActionResult> GetByExamSlot(
            Guid examSlotId,
            [FromQuery] string? search,
            [FromQuery] string? sort,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            if (!ValidatePaging(page, pageSize)) return BadPagedRequest("Page and pageSize must be greater than 0.");
            try
            {
                var (items, total) = await _service.GetAllExamparticipationsAsync(search, sort, page, pageSize, examSlotId);
                var response = ApiPagedResponse<ExamParticipationResponseDto>.OnPagedSuccess(items, page, pageSize, total, "Exam participations retrieved successfully.");
                return Ok(response);
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        // Get Exam Participation By Id
        // Truyền dữ liệu: route id.
        // Điều kiện: user đã đăng nhập; SuperAdmin xem tất cả, SchoolAdmin/Lecturer cùng institution/class, Student chỉ xem participation của chính mình.
        [HttpGet("{id:guid}")]
        [SupabaseAuthorize]
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

        // Create Exam Participation
        // Truyền dữ liệu: body examSlotId, studentId, billingTransId, actualStart, actualEnd, status, disqualifiedReason, recordingVideoPath, identitySnapshotPath.
        // Điều kiện: SuperAdmin hoặc Student chính chủ; Student chỉ tạo participation cho class mình đã enrollment.
        [HttpPost]
        [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.Student)]
        public async Task<IActionResult> Create([FromBody] CreateExamParticipationDto dto)
        {
            try
            {
                var result = await _service.CreateAsync(dto);
                return CreatedSingle(result, "Exam participation created successfully.");
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        // Update Exam Participation
        // Truyền dữ liệu: route id, body actualStart, actualEnd, status, disqualifiedReason, recordingVideoPath, identitySnapshotPath.
        // Điều kiện: SuperAdmin, SchoolAdmin cùng institution hoặc Student chính chủ; exam participation phải tồn tại.
        [HttpPut("{id:guid}")]
        [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin, AppRole.Student)]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateExamParticipationDto dto)
        {
            try
            {
                var success = await _service.UpdateAsync(id, dto);
                if (!success) return NotFound(ApiResponse<object>.OnFail("Exam participation not found."));
                return Ok(ApiResponse<object>.OnSuccess(null!, "Exam participation updated successfully."));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        // Update Exam Participation Status
        // Truyền dữ liệu: route id, body status.
        // Điều kiện: SuperAdmin, SchoolAdmin cùng institution hoặc Student chính chủ; exam participation phải tồn tại.
        [HttpPut("{id:guid}/status")]
        [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin, AppRole.Student)]
        public async Task<IActionResult> UpdateExamParticipationStatus(Guid id, [FromBody] UpdateExamParticipationStatusDto dto)
        {
            try
            {
                var success = await _service.UpdateAsyncOnlyExamPartipationStatus(id, dto);
                if (!success) return NotFound(ApiResponse<object>.OnFail("Exam participation not found."));
                return Ok(ApiResponse<object>.OnSuccess(null!, "Exam participation status updated successfully."));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        // Delete Exam Participation
        // Truyền dữ liệu: route id.
        // Điều kiện: SuperAdmin, SchoolAdmin cùng institution hoặc Student chính chủ; exam participation phải tồn tại.
        [HttpDelete("{id:guid}")]
        [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin, AppRole.Student)]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var success = await _service.DeleteAsync(id);
                if (!success) return NotFound(ApiResponse<object>.OnFail("Exam participation not found."));
                return Ok(ApiResponse<object>.OnSuccess(null!, "Exam participation deleted successfully."));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        // Join Exam
        // Truyền dữ liệu: route id là participationId.
        // Điều kiện: role Student; Student phải là chủ participation; participation không được Submitted hoặc Disqualified.
        [HttpPost("{id:guid}/join")]
        [SupabaseAuthorize(AppRole.Student)]
        public async Task<IActionResult> Join(Guid id)
        {
            try
            {
                var result = await _workflowService.JoinAsync(id);
                return Ok(ApiResponse<object>.OnSuccess(result, "Student joined exam successfully."));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        // Send Exam Heartbeat
        // Truyền dữ liệu: route id là participationId, body clientTime.
        // Điều kiện: role Student; Student phải là chủ participation; participation phải đang Joined.
        [HttpPost("{id:guid}/heartbeat")]
        [SupabaseAuthorize(AppRole.Student)]
        public async Task<IActionResult> Heartbeat(Guid id, [FromBody] ExamHeartbeatRequestDto? dto)
        {
            try
            {
                var result = await _workflowService.HeartbeatAsync(id, dto?.ClientTime);
                return Ok(ApiResponse<object>.OnSuccess(result, "Exam heartbeat received."));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        // Submit Exam
        // Truyền dữ liệu: route id là participationId, body recordingVideoPath.
        // Điều kiện: role Student; Student phải là chủ participation; participation phải đang Joined.
        [HttpPost("{id:guid}/submit")]
        [SupabaseAuthorize(AppRole.Student)]
        public async Task<IActionResult> Submit(Guid id, [FromBody] ExamSubmitRequestDto? dto)
        {
            try
            {
                var result = await _workflowService.SubmitAsync(id, dto?.RecordingVideoPath);
                return Ok(ApiResponse<object>.OnSuccess(result, "Exam submitted successfully."));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        // Leave Exam
        // Truyền dữ liệu: route id là participationId, body reason.
        // Điều kiện: role Student; Student phải là chủ participation; participation phải đang Joined.
        [HttpPost("{id:guid}/leave")]
        [SupabaseAuthorize(AppRole.Student)]
        public async Task<IActionResult> Leave(Guid id, [FromBody] ExamLeaveRequestDto? dto)
        {
            try
            {
                var result = await _workflowService.LeaveAsync(id, dto?.Reason);
                return Ok(ApiResponse<object>.OnSuccess(result, "Student left exam successfully."));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        // Disqualify Student From Exam
        // Truyền dữ liệu: route id là participationId, body reason.
        // Điều kiện: Lecturer của class, SchoolAdmin cùng institution, SuperAdmin hoặc Student chính chủ; participation phải đang Joined.
        [HttpPost("{id:guid}/disqualify")]
        [SupabaseAuthorize(AppRole.Student, AppRole.Lecturer, AppRole.SchoolAdmin, AppRole.SuperAdmin)]
        public async Task<IActionResult> Disqualify(Guid id, [FromBody] ExamDisqualifyRequestDto dto)
        {
            try
            {
                var result = await _workflowService.DisqualifyAsync(id, dto.Reason);
                return Ok(ApiResponse<object>.OnSuccess(result, "Student disqualified successfully."));
            }
            catch (Exception ex) { return HandleException(ex); }
        }
    }
}
