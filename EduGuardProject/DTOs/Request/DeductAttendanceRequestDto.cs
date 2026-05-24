namespace EduGuardProject.DTOs.Request
{
    public class DeductAttendanceRequestDto
    {
        public Guid WalletId { get; set; }
        public Guid AttendanceSessionId { get; set; }
        public int StudentCount { get; set; }
    }
}
