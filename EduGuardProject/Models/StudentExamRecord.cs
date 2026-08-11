using System.ComponentModel.DataAnnotations.Schema;

namespace EduGuardProject.Models;

public partial class StudentExamRecord
{
    public Guid Id { get; set; }

    public Guid ExamSlotId { get; set; }

    public Guid StudentId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public string? ExamRecord { get; set; }

    public decimal? FinalScore { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public int? DurationSeconds { get; set; }

    [Column("status")]
    public StudentExamRecordStatus Status { get; set; } = StudentExamRecordStatus.Marked;

    public virtual ExamSlot ExamSlot { get; set; } = null!;

    public virtual User Student { get; set; } = null!;
}
