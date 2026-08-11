using System.ComponentModel.DataAnnotations.Schema;

namespace EduGuardProject.Models;

public partial class ExamSlot
{
    public Guid Id { get; set; }

    public Guid ClassId { get; set; }

    public Guid CreatedBy { get; set; }

    [Column("exam_name")]
    public string ExamName { get; set; } = null!;

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public int ExpectedDurationMinutes { get; set; }

    [Column("status")]
    public ExamSlotStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Lecturer assigned to proctor this specific exam slot, if different from the class's own lecturer.
    /// </summary>
    public Guid? ProctorId { get; set; }

    public virtual Class Class { get; set; } = null!;

    [NotMapped]
    public User? Lecturer => Class?.Lecturer;

    public virtual User CreatedByNavigation { get; set; } = null!;

    public virtual User? ProctorNavigation { get; set; }

    public virtual ICollection<ExamParticipation> ExamParticipations { get; set; } = new List<ExamParticipation>();

    public virtual ICollection<ExamQuestion> ExamQuestions { get; set; } = new List<ExamQuestion>();

    public virtual ICollection<StudentExamRecord> StudentExamRecords { get; set; } = new List<StudentExamRecord>();
}
