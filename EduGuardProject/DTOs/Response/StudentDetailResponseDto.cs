using EduGuardProject.Models;

namespace EduGuardProject.DTOs.Response
{
    public class StudentDetailResponseDto
    {
        // Thông tin cơ bản
        public Guid Id { get; set; }
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? StudentCode { get; set; }
        public string? Phone { get; set; }
        public UserStatus Status { get; set; }
        public Guid? InstitutionId { get; set; }
        public DateTime CreatedAt { get; set; }

        // Trạng thái đăng ký khuôn mặt
        public BiometricStatusDto Biometric { get; set; } = new();

        // Kết quả các bài thi (StudentExamRecord — đã có điểm)
        public List<StudentExamResultDto> ExamResults { get; set; } = new();

        // Các kỳ thi đã tham gia (ExamParticipation — trạng thái tham gia, kể cả chưa có điểm)
        public List<StudentExamParticipationDto> ExamParticipations { get; set; } = new();

        // Lịch sử điểm danh
        public List<StudentAttendanceHistoryDto> AttendanceHistory { get; set; } = new();
    }

    public class BiometricStatusDto
    {
        public bool HasActiveBiometric { get; set; }
        public int ActiveVectorCount { get; set; } // hiện tại kỳ vọng 0 hoặc 3
        public BiometricReqStatus? LatestRequestStatus { get; set; }
        public DateTime? LatestRequestReviewedAt { get; set; }
        public string? LatestRequestReason { get; set; }
    }

    public class StudentExamResultDto
    {
        public Guid Id { get; set; }
        public Guid ExamSlotId { get; set; }
        public string? ExamName { get; set; }
        public string? CourseName { get; set; }
        public decimal? FinalScore { get; set; }
        public StudentExamRecordStatus Status { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public int? DurationSeconds { get; set; }
    }

    public class StudentExamParticipationDto
    {
        public Guid Id { get; set; }
        public Guid ExamSlotId { get; set; }
        public string? ExamName { get; set; }
        public string? CourseName { get; set; }
        public ParticipationStatus Status { get; set; }
        public DateTime? ActualStart { get; set; }
        public DateTime? ActualEnd { get; set; }
        public string? DisqualifiedReason { get; set; }
        public bool IdentityVerified { get; set; }
    }

    public class StudentAttendanceHistoryDto
    {
        public Guid Id { get; set; }
        public Guid SessionId { get; set; }
        public Guid ClassId { get; set; }
        public string? CourseName { get; set; }
        public AttendanceStatus Status { get; set; }
        public AttendanceMethod Method { get; set; }
        public DateTime? CheckinAt { get; set; }
    }
}