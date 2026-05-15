using System;
using System.Collections.Generic;

namespace EduGuardProject.Models;

public partial class AttendanceRecord
{
    public Guid Id { get; set; }

    public Guid SessionId { get; set; }

    public Guid StudentId { get; set; }

    public double? ConfidenceScore { get; set; }

    /// <summary>
    /// Bucket: attendance-snapshots
    /// </summary>
    public string? SnapshotPath { get; set; }

    public DateTime? CheckinAt { get; set; }

    public Guid? AdjustedBy { get; set; }

    public DateTime? AdjustedAt { get; set; }

    public virtual User? AdjustedByNavigation { get; set; }

    public virtual AttendanceSession Session { get; set; } = null!;

    public virtual User Student { get; set; } = null!;
}
