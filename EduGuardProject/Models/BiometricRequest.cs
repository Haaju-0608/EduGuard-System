using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EduGuardProject.Models;

public partial class BiometricRequest
{
    public Guid Id { get; set; }

    [Required]
    public Guid StudentId { get; set; }

    public Guid? ApprovedBy { get; set; }

    [Required]
    public string Reason { get; set; } = null!;

    [Column("status")]
    public BiometricReqStatus Status { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User? ApprovedByNavigation { get; set; }

    public virtual ICollection<BiometricDatum> BiometricData { get; set; } = new List<BiometricDatum>();

    public virtual User Student { get; set; } = null!;
}
