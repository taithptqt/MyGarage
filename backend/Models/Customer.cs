using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class Customer
{
    public long Id { get; set; }

    public string FullName { get; set; } = null!;

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? PasswordHash { get; set; }

    public long? TierId { get; set; }

    public decimal? TotalSpending { get; set; }

    public int? NoShowCount { get; set; }

    public bool? IsActive { get; set; }

    public string? Note { get; set; }

    public string? SecurityStamp { get; set; }

    public int? AccessFailedCount { get; set; }

    public bool? IsLockedOut { get; set; }

    public DateTime? LockoutEnd { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public DateTime? CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public bool? IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public long? DeletedBy { get; set; }

    public string? GoogleId { get; set; }

    public string Type { get; set; } = null!;

    public virtual ICollection<AiCustomerInsight> AiCustomerInsights { get; set; } = new List<AiCustomerInsight>();

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public virtual ICollection<BookingGroup> BookingGroups { get; set; } = new List<BookingGroup>();

    public virtual ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();

    public virtual Staff? CreatedByNavigation { get; set; }

    public virtual ICollection<CustomerFeedback> CustomerFeedbacks { get; set; } = new List<CustomerFeedback>();

    public virtual ICollection<CustomerVehicle> CustomerVehicles { get; set; } = new List<CustomerVehicle>();

    public virtual ICollection<CustomerVoucher> CustomerVouchers { get; set; } = new List<CustomerVoucher>();

    public virtual Staff? DeletedByNavigation { get; set; }

    public virtual ICollection<JournalEntryLine> JournalEntryLines { get; set; } = new List<JournalEntryLine>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<RetailOrder> RetailOrders { get; set; } = new List<RetailOrder>();

    public virtual ICollection<ServiceOrder> ServiceOrders { get; set; } = new List<ServiceOrder>();

    public virtual CustomerTier? Tier { get; set; }

    public virtual Staff? UpdatedByNavigation { get; set; }

    public virtual ICollection<VoucherRedemption> VoucherRedemptions { get; set; } = new List<VoucherRedemption>();

    public virtual ICollection<WorkSlot> WorkSlots { get; set; } = new List<WorkSlot>();
}
