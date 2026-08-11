namespace EduGuardProject.DTOs.Response
{
    public class WalletResponseDto
    {
        public Guid Id { get; set; }
        public Guid InstitutionId { get; set; }
        public decimal Balance { get; set; }
        public string Currency { get; set; } = "VND";
        public decimal LowBalanceThreshold { get; set; }
    }

    public class TransactionResponseDto
    {
        public Guid Id { get; set; }
        public Guid WalletId { get; set; }
        public decimal Amount { get; set; }
        public string Type { get; set; } = null!; // TOP_UP, ATTENDANCE_FEE, PROCTORING_FEE
        public string Status { get; set; } = null!; // SUCCESS, PENDING, FAILED
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
    }

    //Phần VNPay return
    public class VnPayReturnResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public decimal? Amount { get; set; }
        public string? TxnRef { get; set; }
    }
}
