namespace EduGuardProject.DTOs.Request
{
   
    // DTO request khi Admin nạp tiền cho Trường
    public class TopUpRequestDto
    {
        public Guid InstitutionId { get; set; }
        public decimal Amount { get; set; }
        public string? Description { get; set; }
    }
}
