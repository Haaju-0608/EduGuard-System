using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EduGuardProject.Models;

public partial class ClassEnrollment
{
    [Required]
    public Guid ClassId { get; set; }

    [Required]
    public Guid StudentId { get; set; }

    [Column("status")]
    public EnrollmentStatus Status { get; set; }

    public DateTime EnrolledAt { get; set; }

    public virtual Class Class { get; set; } = null!;

    public virtual User Student { get; set; } = null!;
}
