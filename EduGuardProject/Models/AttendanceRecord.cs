using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EduGuardProject.Models;

public partial class AttendanceRecord
{
    public Guid Id { get; set; }

    [Required]
    public Guid SessionId { get; set; }

    [Required]
    public Guid StudentId { get; set; }

    [Range(0, 1)]
    public double? ConfidenceScore { get; set; }

    /// <summary>
    /// Bucket: attendance-snapshots
    /// </summary>
    [MaxLength(500)]
    public string? SnapshotPath { get; set; }

    public DateTime? CheckinAt { get; set; }

    public Guid? AdjustedBy { get; set; }

    public DateTime? AdjustedAt { get; set; }

    [Column("method")]
    public AttendanceMethod Method { get; set; }

    [Column("status")]
    public AttendanceStatus Status { get; set; }

    public virtual User? AdjustedByNavigation { get; set; }

    public virtual AttendanceSession Session { get; set; } = null!;

    public virtual User Student { get; set; } = null!;
}
