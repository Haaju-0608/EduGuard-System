namespace EduGuardProject.DTOs.Request
{
    public class DeductProctoringRequestDto
    {
        public Guid WalletId { get; set; }
        public Guid ExamParticipationId { get; set; }
        [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)]
        public int Hours { get; set; }
    }
}
