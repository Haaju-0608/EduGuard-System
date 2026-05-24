namespace EduGuardProject.DTOs.Request
{
    public class DeductProctoringRequestDto
    {
        public Guid WalletId { get; set; }
        public Guid ExamParticipationId { get; set; }
        public int Hours { get; set; }
    }
}
