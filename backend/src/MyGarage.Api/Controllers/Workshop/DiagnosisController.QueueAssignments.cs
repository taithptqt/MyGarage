using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Controllers
{
    public partial class DiagnosisController : ControllerBase
    {
        [HttpPost("service-orders/{serviceOrderId:long}/diagnosis-queues/support-requests")]
        public async Task<IActionResult> RequestSupport(
            long serviceOrderId,
            [FromBody] DiagnosisSupportRequestDto request)
        {
            return ToActionResult(await _queueFacade.RequestSupportAsync(serviceOrderId, request));
        }

        [HttpPost("service-orders/{serviceOrderId:long}/diagnosis-queues/cancel-requests")]
        public async Task<IActionResult> RequestCancelAssignment(
            long serviceOrderId,
            [FromBody] DiagnosisCancelRequestDto request)
        {
            return ToActionResult(await _queueFacade.RequestCancelAssignmentAsync(serviceOrderId, request));
        }

        [HttpPost("service-orders/{serviceOrderId:long}/diagnosis-queues/cancel-requests/decisions")]
        public async Task<IActionResult> DecideCancelAssignment(
            long serviceOrderId,
            [FromBody] DiagnosisCancelDecisionDto request)
        {
            return ToActionResult(await _queueFacade.DecideCancelAssignmentAsync(serviceOrderId, request));
        }

        [HttpPost("service-orders/{serviceOrderId:long}/diagnosis-queues/manager-overrides")]
        public async Task<ActionResult<DiagnosisResponseDto>> ManagerOverrideAssignment(
            long serviceOrderId,
            [FromBody] DiagnosisManagerOverrideDto request)
        {
            return ToActionResult(await _queueFacade.ManagerOverrideAssignmentAsync(serviceOrderId, request));
        }

        [HttpPost("service-orders/{serviceOrderId:long}/diagnosis-queues/supporting-team-selections")]
        public async Task<ActionResult<DiagnosisResponseDto>> SelectSupportingTeamAssignment(
            long serviceOrderId,
            [FromBody] DiagnosisSupportingTeamSelectionRequestDto request)
        {
            return ToActionResult(await _queueFacade.SelectSupportingTeamAssignmentAsync(serviceOrderId, request));
        }
    }
}
