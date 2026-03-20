using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class ApprovalRequestService : GenericService<ApprovalRequest>, IApprovalRequestService
    {
        public ApprovalRequestService(IApprovalRequestRepository repository) : base(repository)
        {
        }
    }
}
