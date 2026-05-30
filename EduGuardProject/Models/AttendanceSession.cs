using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EduGuardProject.Models;

public partial class AttendanceSession
{
    public Guid Id { get; set; }

    [Required]
    public Guid ClassId { get; set; }

    [Required]
    public Guid CreatedBy { get; set; }

    public Guid? BillingTransId { get; set; }

    /// <summary>
    /// Bucket: attendance-videos
    /// </summary>
    [MaxLength(500)]
    public string? VideoPath { get; set; }

    [Range(0, int.MaxValue)]
    public int TotalRecognized { get; set; }

    [Required]
    public DateTime StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    [Column("status")]
    public SessionStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();

    public virtual Transaction? BillingTrans { get; set; }

    public virtual Class Class { get; set; } = null!;

    public virtual User CreatedByNavigation { get; set; } = null!;
}
