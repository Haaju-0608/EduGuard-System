using System;
using System.Collections.Generic;

namespace EduGuardProject.Models;

public partial class Class
{
    public Guid Id { get; set; }

    public Guid InstitutionId { get; set; }

    public Guid LecturerId { get; set; }

    public string CourseName { get; set; } = null!;

    public string? CourseCode { get; set; }

    public string Semester { get; set; } = null!;

    public string AcademicYear { get; set; } = null!;

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual ICollection<AttendanceSession> AttendanceSessions { get; set; } = new List<AttendanceSession>();

    public virtual ICollection<ClassEnrollment> ClassEnrollments { get; set; } = new List<ClassEnrollment>();

    public virtual User? CreatedByNavigation { get; set; }

    public virtual ICollection<ExamSlot> ExamSlots { get; set; } = new List<ExamSlot>();

    public virtual Institution Institution { get; set; } = null!;

    public virtual User Lecturer { get; set; } = null!;

    public virtual User? UpdatedByNavigation { get; set; }
}
