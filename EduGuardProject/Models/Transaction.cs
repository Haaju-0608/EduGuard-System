using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace EduGuardProject.Models;

public partial class Transaction
{
    public Guid Id { get; set; }

    public Guid WalletId { get; set; }

    public Guid? PricingConfigId { get; set; }

    public string? VnpayRef { get; set; }

    public decimal Amount { get; set; }

    [Column("type")]
    public TransactionType Type { get; set; }
    [Column("status")]
    public TransactionStatus Status { get; set; }

    public string? Description { get; set; }

    public DateTime? ProcessedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual AttendanceSession? AttendanceSession { get; set; }

    public virtual ExamParticipation? ExamParticipation { get; set; }

    public virtual PricingConfig? PricingConfig { get; set; }

    public virtual Wallet Wallet { get; set; } = null!;
}
