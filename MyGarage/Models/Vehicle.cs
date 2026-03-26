using MyGarage.Interfaces;

namespace MyGarage.Models;

public partial class Vehicle : IAuditEntity
{
    public long Id { get; set; }

    public string LicensePlate { get; set; } = null!;

    public string? Vin { get; set; }

    public long ModelId { get; set; }

    public string? Color { get; set; }

    public int? Odo { get; set; }

    public DateTime? OdoLastUpdatedAt { get; set; }

    public int? ManufactureYear { get; set; }

    public DateTime? CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public bool? IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public long? DeletedBy { get; set; }

    public virtual ICollection<AiForecast> AiForecasts { get; set; } = new List<AiForecast>();

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public virtual ICollection<Bay> Bays { get; set; } = new List<Bay>();

    public virtual Staff? CreatedByNavigation { get; set; }

    public virtual ICollection<CustomerVehicle> CustomerVehicles { get; set; } = new List<CustomerVehicle>();

    public virtual Staff? DeletedByNavigation { get; set; }

    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    public virtual VehicleModel Model { get; set; } = null!;

    public virtual ICollection<ServiceOrder> ServiceOrders { get; set; } = new List<ServiceOrder>();

    public virtual Staff? UpdatedByNavigation { get; set; }

    public virtual ICollection<VehicleInsurance> VehicleInsurances { get; set; } = new List<VehicleInsurance>();

    public virtual ICollection<WorkSlot> WorkSlots { get; set; } = new List<WorkSlot>();
}
