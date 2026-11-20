using Artefacts.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Artefacts.Api;

/// <summary>
/// Maps expected domain failures to RFC 9457 problem responses. Validation errors keep their camelCase
/// field keys so the frontend can show each message next to its field.
/// </summary>
internal sealed class DomainExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        ProblemDetails? problem = exception switch
        {
            DomainValidationException v => new ValidationProblemDetails(v.Errors.ToDictionary(e => e.Key, e => e.Value))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more fields are invalid.",
            },
            NotFoundException nf => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = $"{nf.Resource} not found.",
                Detail = nf.Message,
            },
            ConflictException c => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "The request conflicts with the current state.",
                Detail = c.Message,
                Extensions = { ["code"] = c.Code },
            },
            BadHttpRequestException b => new ProblemDetails
            {
                Status = b.StatusCode,
                Title = b.StatusCode == StatusCodes.Status413PayloadTooLarge ? "The upload is too large." : "Bad request.",
                Detail = b.Message,
            },
            _ => null,
        };

        if (problem is null) return false;

        http.Response.StatusCode = problem.Status!.Value;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails = problem,
            Exception = exception,
        });
    }
}
