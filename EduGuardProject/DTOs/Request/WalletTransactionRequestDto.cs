using System.ComponentModel.DataAnnotations;

namespace EduGuardProject.DTOs.Request
{

    // DTO request khi Admin nạp tiền cho Trường
    public class TopUpRequestDto
    {
        public Guid InstitutionId { get; set; }

        [Range(typeof(decimal), "0.01", "50000000",
            ErrorMessage = "Amount must be between 0.01 and 50,000,000 VND per transaction.")]
        public decimal Amount { get; set; }

        [MaxLength(500, ErrorMessage = "Description must not exceed 500 characters.")]
        public string? Description { get; set; }
    }
}
