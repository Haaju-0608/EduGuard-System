using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace EduGuardProject.Models;

public partial class Notification
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Title { get; set; } = null!;

    public string Body { get; set; } = null!;

    public bool IsRead { get; set; }

    public Guid? ReferenceId { get; set; }

    [Column("type")]
    public NotificationType Type { get; set; }

    [Column("reference_type")]
    public ReferenceTypeEnum? ReferenceType { get; set; }

    [Column("sent_via")]
    public NotificationChannel SentVia { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
