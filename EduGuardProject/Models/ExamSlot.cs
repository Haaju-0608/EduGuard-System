using System.ComponentModel.DataAnnotations.Schema;

namespace EduGuardProject.Models;

public partial class ExamSlot
{
    public Guid Id { get; set; }

    public Guid ClassId { get; set; }

    public Guid CreatedBy { get; set; }

    public string ExamName { get; set; } = null!;

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public int ExpectedDurationMinutes { get; set; }

    [Column("status")]
    public ExamSlotStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Class Class { get; set; } = null!;

    public virtual User CreatedByNavigation { get; set; } = null!;

    public virtual ICollection<ExamParticipation> ExamParticipations { get; set; } = new List<ExamParticipation>();
}
