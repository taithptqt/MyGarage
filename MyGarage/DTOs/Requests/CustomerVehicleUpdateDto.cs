using System;
namespace MyGarage.DTOs.Requests
{
    public class CustomerVehicleUpdateDto
    {
        public long CustomerId { get; set; }
        public long VehicleId { get; set; }
        public bool? IsCurrentOwner { get; set; }
        public DateOnly? OwnershipStartDate { get; set; }
        public DateOnly? TransferDate { get; set; }
        public string? Note { get; set; }
        public long? CreatedBy { get; set; }
        public long? UpdatedBy { get; set; }
        public bool? IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public long? DeletedBy { get; set; }
    }
}
