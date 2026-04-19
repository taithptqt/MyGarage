using System;

namespace MyGarage.Interfaces
{
    public interface IAuditableEntity
    {
        DateTime? CreatedAt { get; set; }
        long? CreatedBy { get; set; }
        DateTime? UpdatedAt { get; set; }
        long? UpdatedBy { get; set; }
    }
}
