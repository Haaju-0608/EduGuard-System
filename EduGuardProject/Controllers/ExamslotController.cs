using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Filters;
using EduGuardProject.Models;
using EduGuardProject.Services.IServices;
using Microsoft.AspNetCore.Mvc;

namespace EduGuardProject.Controllers
{
    [Route("api/exam-slots")]
    [ApiController]
    [SupabaseAuthorize]
    // Controller quản lý Exam Slot.
    // Mỗi API bên dưới có comment ghi rõ dữ liệu cần truyền và điều kiện được gọi.
    public class ExamslotController : AcademicApiControllerBase
    {
        private readonly IExamSlotServices _service;
        private readonly IExamWorkflowService _workflowService;

        public ExamslotController(IExamSlotServices service, IExamWorkflowService workflowService)
        {
            _service = service;
            _workflowService = workflowService;
        }

        // Get All Exam Slots
        // Truyền dữ liệu: query search, sort, page, pageSize.
        // Điều kiện: user đã đăng nhập; page và pageSize phải lớn hơn 0.
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
                var response = ApiPagedResponse<ExamslotReponseDto>.OnPagedSuccess(items, page, pageSize, total, "Exam slot retrieved successfully.");
                return Ok(response);
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        // Get Exam Slot By Id
        // Truyền dữ liệu: route examId.
        // Điều kiện: user đã đăng nhập; exam slot phải tồn tại.
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

        // Get Scheduled Exam Slot For Student
        // Truyền dữ liệu: route examId, query studentId.
        // Điều kiện: Student chỉ xem chính mình; SchoolAdmin cùng institution; SuperAdmin xem tất cả; exam slot phải có status Scheduled.
        [HttpGet("{examId:guid}/student")]
        [SupabaseAuthorize(AppRole.Student, AppRole.SchoolAdmin, AppRole.SuperAdmin)]
        public async Task<IActionResult> GetByIdForStudent(Guid examId, [FromQuery] Guid studentId)
        {
            try
            {
                if (studentId == Guid.Empty) return BadRequest(ApiResponse<object>.OnFail("Student id is required."));

                var item = await _service.GetByIdForStudentAsync(examId, studentId);
                if (item == null) return NotFound(ApiResponse<object>.OnFail("Exam slot not found."));
                return OkSingle(item, "Exam slot retrieved successfully.");
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        // Get Exam Slots By Class
        // Truyền dữ liệu: route classId.
        // Điều kiện: Student phải đang học class đó; SchoolAdmin cùng institution; SuperAdmin xem tất cả.
        [HttpGet("class/{classId:guid}")]
        [SupabaseAuthorize(AppRole.Student, AppRole.SchoolAdmin, AppRole.SuperAdmin, AppRole.Lecturer)]
        public async Task<IActionResult> GetByClassId(Guid classId)
        {
            try
            {
                if (classId == Guid.Empty) return BadRequest(ApiResponse<object>.OnFail("Class id is required."));

                var items = await _service.GetByClassIdAsync(classId);
                return Ok(ApiResponse<IEnumerable<ExamslotReponseDto>>.OnSuccess(items, "Exam slots retrieved successfully."));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        // Get Exam History By StudentId
        // Truyền dữ liệu: route studentId.
        // Điều kiện: SchoolAdmin chỉ xem student cùng institution; SuperAdmin xem tất cả.
        [HttpGet("GetStudentExamHistory/{studentId:guid}")]
        [SupabaseAuthorize(AppRole.SchoolAdmin, AppRole.SuperAdmin)]
        public async Task<IActionResult> GetByStudentId(Guid studentId)
        {
            try
            {
                if (studentId == Guid.Empty) return BadRequest(ApiResponse<object>.OnFail("Student id is required."));

                var items = await _service.GetByStudentIdAsync(studentId);
                return Ok(ApiResponse<IEnumerable<ExamslotReponseDto>>.OnSuccess(items, "Exam slots retrieved successfully."));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        // Get Exam History For Student
        // Truyền dữ liệu: bearer token.
        // Điều kiện: role Student; trả về các exam slot student hiện tại đã tham gia.
        [HttpGet("GetMyExamHistory")]
        [SupabaseAuthorize(AppRole.Student)]
        public async Task<IActionResult> GetMyExamHistory()
        {
            try
            {
                var items = await _service.GetMyExamHistoryAsync();
                return Ok(ApiResponse<IEnumerable<ExamslotReponseDto>>.OnSuccess(items, "Exam slots retrieved successfully."));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        // Get Exam Realtime State
        // Truyền dữ liệu: route examId.
        // Điều kiện: Lecturer, SchoolAdmin hoặc SuperAdmin có quyền truy cập workflow của exam.
        [HttpGet("{examId:guid}/realtime-state")]
        [SupabaseAuthorize(AppRole.Lecturer, AppRole.SchoolAdmin, AppRole.SuperAdmin)]
        public async Task<IActionResult> GetRealtimeState(Guid examId)
        {
            try
            {
                var state = await _workflowService.GetRealtimeStateAsync(examId);
                return Ok(ApiResponse<object>.OnSuccess(state, "Exam realtime state retrieved successfully."));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        // Create Exam Slot
        // Truyền dữ liệu: body classId, lecturerId, examName, status, expectedDurationMinutes, startTime, endTime.
        // Điều kiện: SchoolAdmin hoặc SuperAdmin; class phải tồn tại và thuộc institution được phép truy cập; lecturerId nếu truyền phải là Lecturer cùng institution.
        [HttpPost]
        [SupabaseAuthorize(AppRole.SchoolAdmin, AppRole.SuperAdmin)]
        public async Task<IActionResult> Create([FromBody] CreateExamSlotDto dto)
        {
            try
            {
                var result = await _service.CreateAsync(dto);
                return CreatedSingle(result, "Exam slot created successfully.");
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        // Update Exam Slot
        // Truyền dữ liệu: route examId, body lecturerId, examName, startTime, endTime, expectedDurationMinutes.
        // Điều kiện: SchoolAdmin hoặc SuperAdmin; exam slot phải tồn tại và thuộc institution được phép truy cập; lecturerId nếu truyền phải là Lecturer cùng institution; response trả về exam slot kèm lecturer.
        [HttpPut("{examId:guid}")]
        [SupabaseAuthorize(AppRole.SchoolAdmin, AppRole.SuperAdmin)]
        public async Task<IActionResult> Update(Guid examId, [FromBody] UpdateExamSlotDto dto)
        {
            try
            {
                var result = await _service.UpdateAsync(examId, dto);
                if (result == null) return NotFound(ApiResponse<object>.OnFail("Exam slot not found."));
                return OkSingle(result, "Exam slot updated successfully.");
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        // Delete Exam Slot
        // Truyền dữ liệu: route examId.
        // Điều kiện: SchoolAdmin hoặc SuperAdmin; exam slot phải tồn tại và thuộc institution được phép truy cập.
        [HttpDelete("{examId:guid}")]
        [SupabaseAuthorize(AppRole.SchoolAdmin, AppRole.SuperAdmin)]
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
