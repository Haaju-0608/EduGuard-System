namespace EduGuardProject.DTOs.Response
{
    public class AiSinglePhotoAttendanceResultDto
    {
        public bool IsMatch { get; set; }

        public Guid SessionId { get; set; }
        public Guid ClassId { get; set; }
        public string? ClassName { get; set; }
        public Guid? ExamSlotId { get; set; }

        public Guid? StudentId { get; set; }
        public string? StudentName { get; set; }
        public string? StudentCode { get; set; }

        public bool MatchedButWrongClass { get; set; }

        public string? Message { get; set; }
        public AttendanceRecordResponseDto? Record { get; set; } 
    }
}