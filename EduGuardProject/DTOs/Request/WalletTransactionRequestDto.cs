namespace EduGuardProject.DTOs.Request
{

    // DTO request khi Admin nạp tiền cho Trường
    public class TopUpRequestDto
    {
        public Guid InstitutionId { get; set; }
        [System.ComponentModel.DataAnnotations.Range(
            typeof(decimal),
            "0.01",
            "79228162514264337593543950335")]
        public decimal Amount { get; set; }
        public string? Description { get; set; }
    }
}
