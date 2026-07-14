using EduGuardProject.Models;

namespace EduGuardProject.DTOs.Response
{
    public class InstitutionResponseDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public string SubDomain { get; set; } = null!;
        public string ContactEmail { get; set; } = null!;
        public DateTime? SubscriptionExpiresAt { get; set; }
        public BillingModel BillingModel { get; set; }
        public InstitutionStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
