using EduGuardProject.Models;

namespace EduGuardProject.DTOs.Request
{
    public class RenewSubscriptionDto
    {
        public BillingModel BillingModel { get; set; } // Monthly hoặc Yearly - do School Admin chọn lúc renew
    }
}
