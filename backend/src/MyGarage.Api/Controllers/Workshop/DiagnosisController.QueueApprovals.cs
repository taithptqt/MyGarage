using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests;

namespace MyGarage.Controllers
{
    public partial class DiagnosisController : ControllerBase
    {
        [HttpPost("service-orders/{serviceOrderId:long}/diagnosis-queues/technical-approvals")]
        public async Task<IActionResult> ApproveTechnical(
            long serviceOrderId,
            [FromBody] DiagnosisTechnicalApprovalRequestDto request)
        {
            return ToActionResult(await _approvalFacade.ApproveTechnicalAsync(serviceOrderId, request));
        }

        [HttpPost("service-orders/{serviceOrderId:long}/diagnosis-queues/technical-rejections")]
        public async Task<IActionResult> RejectTechnical(
            long serviceOrderId,
            [FromBody] DiagnosisTechnicalApprovalRequestDto request)
        {
            return ToActionResult(await _approvalFacade.RejectTechnicalAsync(serviceOrderId, request));
        }

        [HttpPost("service-orders/{serviceOrderId:long}/diagnosis-queues/version-clones")]
        public async Task<IActionResult> CloneRejectedVersion(
            long serviceOrderId,
            [FromBody] DiagnosisCloneVersionRequestDto? request)
        {
            return ToActionResult(await _approvalFacade.CloneRejectedVersionAsync(serviceOrderId, request));
        }

        [HttpPost("service-orders/{serviceOrderId:long}/diagnosis-queues/final-approvals")]
        public async Task<IActionResult> ApproveFinal(
            long serviceOrderId,
            [FromBody] DiagnosisFinalApprovalRequestDto request)
        {
            return ToActionResult(await _approvalFacade.ApproveFinalAsync(serviceOrderId, request));
        }

        [HttpPost("service-orders/{serviceOrderId:long}/diagnosis-queues/foreman-decisions")]
        public async Task<IActionResult> ForemanDecision(
            long serviceOrderId,
            [FromBody] DiagnosisForemanDecisionRequestDto request)
        {
            return ToActionResult(await _approvalFacade.ForemanDecisionAsync(serviceOrderId, request));
        }
    }
}
