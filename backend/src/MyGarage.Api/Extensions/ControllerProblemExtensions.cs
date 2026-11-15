using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace MyGarage.Controllers
{
    public static class ControllerProblemExtensions
    {
        public static ObjectResult BadRequestProblem(
            this ControllerBase controller,
            string message,
            string? errorCode = null,
            IDictionary<string, object?>? extensions = null) =>
            controller.ProblemMessage(StatusCodes.Status400BadRequest, message, errorCode, extensions);

        public static ObjectResult UnauthorizedProblem(this ControllerBase controller, string message, string? errorCode = null) =>
            controller.ProblemMessage(StatusCodes.Status401Unauthorized, message, errorCode);

        public static ObjectResult ForbiddenProblem(this ControllerBase controller, string message, string? errorCode = null) =>
            controller.ProblemMessage(StatusCodes.Status403Forbidden, message, errorCode);

        public static ObjectResult NotFoundProblem(this ControllerBase controller, string message, string? errorCode = null) =>
            controller.ProblemMessage(StatusCodes.Status404NotFound, message, errorCode);

        public static ObjectResult ConflictProblem(this ControllerBase controller, string message, string? errorCode = null) =>
            controller.ProblemMessage(StatusCodes.Status409Conflict, message, errorCode);

        public static ObjectResult UnprocessableProblem(this ControllerBase controller, string message, string? errorCode = null) =>
            controller.ProblemMessage(StatusCodes.Status422UnprocessableEntity, message, errorCode);

        public static ObjectResult ProblemMessage(
            this ControllerBase controller,
            int statusCode,
            string message,
            string? errorCode = null,
            IDictionary<string, object?>? extensions = null)
        {
            ArgumentNullException.ThrowIfNull(controller);

            var problemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = ReasonPhrases.GetReasonPhrase(statusCode),
                Detail = message,
                Type = GetProblemType(statusCode),
                Instance = controller.HttpContext?.Request?.Path.Value
            };

            problemDetails.Extensions["message"] = message;
            problemDetails.Extensions["statusCode"] = statusCode;

            if (!string.IsNullOrWhiteSpace(errorCode))
            {
                problemDetails.Extensions["errorCode"] = errorCode;
            }

            var traceId = controller.HttpContext?.TraceIdentifier;
            if (!string.IsNullOrWhiteSpace(traceId))
            {
                problemDetails.Extensions["traceId"] = traceId;
            }

            if (extensions is not null)
            {
                foreach (var (key, value) in extensions)
                {
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        problemDetails.Extensions[key] = value;
                    }
                }
            }

            var result = new ObjectResult(problemDetails)
            {
                StatusCode = statusCode
            };
            result.ContentTypes.Add("application/problem+json");
            return result;
        }

        private static string GetProblemType(int statusCode) => statusCode switch
        {
            StatusCodes.Status400BadRequest => "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            StatusCodes.Status401Unauthorized => "https://tools.ietf.org/html/rfc9110#section-15.5.2",
            StatusCodes.Status403Forbidden => "https://tools.ietf.org/html/rfc9110#section-15.5.4",
            StatusCodes.Status404NotFound => "https://tools.ietf.org/html/rfc9110#section-15.5.5",
            StatusCodes.Status409Conflict => "https://tools.ietf.org/html/rfc9110#section-15.5.10",
            StatusCodes.Status422UnprocessableEntity => "https://tools.ietf.org/html/rfc9110#section-15.5.21",
            StatusCodes.Status500InternalServerError => "https://tools.ietf.org/html/rfc9110#section-15.6.1",
            StatusCodes.Status503ServiceUnavailable => "https://tools.ietf.org/html/rfc9110#section-15.6.4",
            StatusCodes.Status504GatewayTimeout => "https://tools.ietf.org/html/rfc9110#section-15.6.5",
            _ => $"https://httpstatuses.io/{statusCode}"
        };
    }
}
