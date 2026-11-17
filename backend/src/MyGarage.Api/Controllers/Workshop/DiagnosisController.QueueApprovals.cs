using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests;
using MyGarage.Extensions;

namespace MyGarage.Controllers
{
    public partial class DiagnosisController : ControllerBase
    {
        [HttpPost("service-orders/{serviceOrderId:long}/diagnosis-queues/technical-approvals")]
        [HttpPost("~/api/v1/service-orders/{serviceOrderId:long}/diagnosis-queues/technical-approvals")]
        public async Task<IActionResult> ApproveTechnical(
            long serviceOrderId,
            [FromBody] DiagnosisTechnicalApprovalRequestDto request)
        {
            if (request == null || request.TeamLeaderId <= 0 || request.TeamLeaderId != this.GetCurrentUserId() ||
                !this.IsStaffWithAnyKeyword("ADMIN", "MANAGER", "SERVICE_MANAGER", "FOREMAN", "TEAM_LEAD", "QUAN_DOC"))
                return Forbid();
            var order = await _diagnosisService.GetServiceOrderAsync(serviceOrderId);
            if (order == null) return NotFound();
            if (!this.IsStaffWithAnyKeyword("ADMIN") && this.GetCurrentBranchId() != order.ExecuteBranchId) return Forbid();
            return ToActionResult(await _approvalFacade.ApproveTechnicalAsync(serviceOrderId, request));
        }

        [HttpPost("service-orders/{serviceOrderId:long}/diagnosis-queues/technical-rejections")]
        [HttpPost("~/api/v1/service-orders/{serviceOrderId:long}/diagnosis-queues/technical-rejections")]
        public async Task<IActionResult> RejectTechnical(
            long serviceOrderId,
            [FromBody] DiagnosisTechnicalApprovalRequestDto request)
        {
            if (request == null || request.TeamLeaderId <= 0 || request.TeamLeaderId != this.GetCurrentUserId() ||
                !this.IsStaffWithAnyKeyword("ADMIN", "MANAGER", "SERVICE_MANAGER", "FOREMAN", "TEAM_LEAD", "QUAN_DOC"))
                return Forbid();
            var order = await _diagnosisService.GetServiceOrderAsync(serviceOrderId);
            if (order == null) return NotFound();
            if (!this.IsStaffWithAnyKeyword("ADMIN") && this.GetCurrentBranchId() != order.ExecuteBranchId) return Forbid();
            return ToActionResult(await _approvalFacade.RejectTechnicalAsync(serviceOrderId, request));
        }

        [HttpPost("service-orders/{serviceOrderId:long}/diagnosis-queues/version-clones")]
        [HttpPost("~/api/v1/service-orders/{serviceOrderId:long}/diagnosis-queues/version-clones")]
        public async Task<IActionResult> CloneRejectedVersion(
            long serviceOrderId,
            [FromBody] DiagnosisCloneVersionRequestDto? request)
        {
            if (request == null || request.ActorId <= 0 || request.ActorId != this.GetCurrentUserId() ||
                !this.IsStaffWithAnyKeyword("ADMIN", "MANAGER", "SERVICE_MANAGER", "FOREMAN", "TEAM_LEAD", "QUAN_DOC")) return Forbid();
            var order = await _diagnosisService.GetServiceOrderAsync(serviceOrderId);
            if (order == null) return NotFound();
            if (!this.IsStaffWithAnyKeyword("ADMIN") && this.GetCurrentBranchId() != order.ExecuteBranchId) return Forbid();
            return ToActionResult(await _approvalFacade.CloneRejectedVersionAsync(serviceOrderId, request));
        }

        [HttpPost("service-orders/{serviceOrderId:long}/diagnosis-queues/final-approvals")]
        [HttpPost("~/api/v1/service-orders/{serviceOrderId:long}/diagnosis-queues/final-approvals")]
        public async Task<IActionResult> ApproveFinal(
            long serviceOrderId,
            [FromBody] DiagnosisFinalApprovalRequestDto request)
        {
            if (request == null || request.TeamLeaderId <= 0 || request.TeamLeaderId != this.GetCurrentUserId() ||
                !this.IsStaffWithAnyKeyword("ADMIN", "MANAGER", "SERVICE_MANAGER", "FOREMAN", "QUAN_DOC")) return Forbid();
            var order = await _diagnosisService.GetServiceOrderAsync(serviceOrderId);
            if (order == null) return NotFound();
            if (!this.IsStaffWithAnyKeyword("ADMIN") && this.GetCurrentBranchId() != order.ExecuteBranchId) return Forbid();
            return ToActionResult(await _approvalFacade.ApproveFinalAsync(serviceOrderId, request));
        }

        [HttpPost("service-orders/{serviceOrderId:long}/diagnosis-queues/foreman-decisions")]
        [HttpPost("~/api/v1/service-orders/{serviceOrderId:long}/diagnosis-queues/foreman-decisions")]
        public async Task<IActionResult> ForemanDecision(
            long serviceOrderId,
            [FromBody] DiagnosisForemanDecisionRequestDto request)
        {
            if (request == null || request.ForemanId <= 0 || request.ForemanId != this.GetCurrentUserId() ||
                !this.IsStaffWithAnyKeyword("ADMIN", "MANAGER", "SERVICE_MANAGER", "FOREMAN", "QUAN_DOC")) return Forbid();
            var order = await _diagnosisService.GetServiceOrderAsync(serviceOrderId);
            if (order == null) return NotFound();
            if (!this.IsStaffWithAnyKeyword("ADMIN") && this.GetCurrentBranchId() != order.ExecuteBranchId) return Forbid();
            return ToActionResult(await _approvalFacade.ForemanDecisionAsync(serviceOrderId, request));
        }
    }
}
