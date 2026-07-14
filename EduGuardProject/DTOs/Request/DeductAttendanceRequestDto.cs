namespace EduGuardProject.DTOs.Request
{
    public class DeductAttendanceRequestDto
    {
        public Guid WalletId { get; set; }
        public Guid AttendanceSessionId { get; set; }
        [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)]
        public int StudentCount { get; set; }
    }
}
