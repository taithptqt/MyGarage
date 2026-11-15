using System;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Enums;
using MyGarage.Interfaces;

namespace MyGarage.Controllers
{
    public partial class DiagnosisController : ControllerBase
    {
        private static string NormalizeStatus(string? rawStatus)
        {
            var normalized = (rawStatus ?? string.Empty).Trim().ToUpperInvariant();
            return Enum.TryParse<DiagnosisStatus>(normalized, true, out var parsed)
                ? parsed.ToString()
                : DiagnosisStatusPending;
        }

        private static string? NormalizeDispatchTag(string? rawTag)
        {
            if (string.IsNullOrWhiteSpace(rawTag))
            {
                return null;
            }

            return rawTag.Trim().ToUpperInvariant();
        }

        private IActionResult ToActionResult(DiagnosisCommandResult result) => result.StatusCode switch
        {
            200 => Ok(result.Payload),
            400 => BadRequest(result.Payload),
            403 => Forbid(),
            404 => result.Payload == null ? NotFound() : NotFound(result.Payload),
            409 => Conflict(result.Payload),
            _ => StatusCode(result.StatusCode, result.Payload)
        };

        private ActionResult<T> ToActionResult<T>(DiagnosisCommandResult<T> result) => result.StatusCode switch
        {
            200 => Ok(result.Data),
            400 => BadRequest(result.ErrorPayload),
            403 => Forbid(),
            404 => result.ErrorPayload == null ? NotFound() : NotFound(result.ErrorPayload),
            409 => Conflict(result.ErrorPayload),
            _ => StatusCode(result.StatusCode, result.ErrorPayload)
        };
    }
}
