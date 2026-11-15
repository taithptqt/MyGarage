using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Controllers
{
    public partial class DiagnosisController : ControllerBase
    {
        [HttpPost("service-orders/{serviceOrderId:long}/diagnosis-queues")]
        public async Task<ActionResult<DiagnosisResponseDto>> RequestDiagnosis(
            long serviceOrderId,
            [FromBody] DiagnosisQueueCreateRequestDto? request)
        {
            return ToActionResult(await _queueFacade.RequestDiagnosisAsync(serviceOrderId, request));
        }

        [HttpPatch("service-orders/{serviceOrderId:long}/diagnosis-queues")]
        public async Task<ActionResult<DiagnosisResponseDto>> PatchRequestAssignment(
            long serviceOrderId,
            [FromBody] DiagnosisAcceptRequestDto request)
        {
            return ToActionResult(await _queueFacade.AssignRequestAsync(serviceOrderId, request));
        }

        [HttpPost("service-orders/{serviceOrderId:long}/diagnosis-queues/starts")]
        public async Task<ActionResult<DiagnosisResponseDto>> StartDiagnosis(
            long serviceOrderId,
            [FromBody] DiagnosisStartRequestDto request)
        {
            return ToActionResult(await _queueFacade.StartDiagnosisAsync(serviceOrderId, request));
        }

        [HttpDelete("service-orders/{serviceOrderId:long}/diagnosis-queues")]
        public async Task<IActionResult> DeleteRequest(long serviceOrderId)
        {
            return ToActionResult(await _queueFacade.DeleteRequestAsync(serviceOrderId));
        }

        [HttpPatch("{id:long}")]
        public async Task<ActionResult<DiagnosisResponseDto>> PatchStatus(long id, [FromBody] DiagnosisStatusPatchDto dto)
        {
            var status = NormalizeStatus(dto?.Status);
            if (status != DiagnosisStatusCompleted)
            {
                return this.BadRequestProblem("Chỉ hỗ trợ chuyển trạng thái sang COMPLETED.");
            }

            return ToActionResult(await _crudFacade.CompleteAsync(id, dto?.Symptom, dto?.RootCause, dto?.Severity));
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _diagnosisService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }

            return NoContent();
        }
    }
}
