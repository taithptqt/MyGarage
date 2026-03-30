using System.Collections.Generic;

namespace MyGarage.DTOs.Requests
{
    public class VoucherCampaignAssignVouchersDto
    {
        public List<long>? VoucherIds { get; set; }
        public bool ReplaceExisting { get; set; } = true;
        public long? UpdatedBy { get; set; }
    }
}
