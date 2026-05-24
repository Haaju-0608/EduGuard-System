using EduGuardProject.Models;

namespace EduGuardProject.DTOs.Request
{
    //hứng dữ liệu khi tạo mới
    public class CreateInstitutionDto
    {
        public string Name { get; set; } = null!;
        public string SubDomain { get; set; } = null!;
        public string ContactEmail { get; set; } = null!;
        public BillingModel BillingModel { get; set; }
        public InstitutionStatus Status { get; set; } = InstitutionStatus.Active;
    }

    //hứng dữ liệu khi cập nhật
    public class UpdateInstitutionDto
    {
        public string Name { get; set; } = null!;
        public string SubDomain { get; set; } = null!;
        public string ContactEmail { get; set; } = null!;
        public BillingModel BillingModel { get; set; }
        public InstitutionStatus Status { get; set; }
    }

}
