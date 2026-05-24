using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace EduGuardProject.Models;

public partial class User
{
    public Guid Id { get; set; }

    public Guid? InstitutionId { get; set; }

    public string? StudentCode { get; set; }

    public string Email { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string? Phone { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    [Column("status")]
    public UserStatus Status { get; set; }

    [Column("role")]
    public AppRole Role { get; set; } 

    public virtual ICollection<AttendanceRecord> AttendanceRecordAdjustedByNavigations { get; set; } = new List<AttendanceRecord>();

    public virtual ICollection<AttendanceRecord> AttendanceRecordStudents { get; set; } = new List<AttendanceRecord>();

    public virtual ICollection<AttendanceSession> AttendanceSessions { get; set; } = new List<AttendanceSession>();

    public virtual BiometricDatum? BiometricDatum { get; set; }

    public virtual ICollection<BiometricRequest> BiometricRequestApprovedByNavigations { get; set; } = new List<BiometricRequest>();

    public virtual ICollection<BiometricRequest> BiometricRequestStudents { get; set; } = new List<BiometricRequest>();

    public virtual ICollection<Class> ClassCreatedByNavigations { get; set; } = new List<Class>();

    public virtual ICollection<ClassEnrollment> ClassEnrollments { get; set; } = new List<ClassEnrollment>();

    public virtual ICollection<Class> ClassLecturers { get; set; } = new List<Class>();

    public virtual ICollection<Class> ClassUpdatedByNavigations { get; set; } = new List<Class>();

    public virtual ICollection<ExamParticipation> ExamParticipations { get; set; } = new List<ExamParticipation>();

    public virtual ICollection<ExamSlot> ExamSlots { get; set; } = new List<ExamSlot>();

    public virtual Institution? Institution { get; set; }

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<PricingConfig> PricingConfigCreatedByNavigations { get; set; } = new List<PricingConfig>();

    public virtual ICollection<PricingConfig> PricingConfigUpdatedByNavigations { get; set; } = new List<PricingConfig>();

    public virtual ICollection<ViolationLog> ViolationLogs { get; set; } = new List<ViolationLog>();
}
