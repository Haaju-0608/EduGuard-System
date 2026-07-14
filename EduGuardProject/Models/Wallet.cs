using System;
using System.Collections.Generic;

namespace EduGuardProject.Models;

public partial class Wallet
{
    public Guid Id { get; set; }

    public Guid InstitutionId { get; set; }

    public decimal Balance { get; set; }

    public string Currency { get; set; } = null!;

    public decimal LowBalanceThreshold { get; set; }

    public DateTime? LowBalanceAlertSentAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Institution Institution { get; set; } = null!;

    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
