using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartQuote.API.Shared.Domain;

namespace SmartQuote.Shared.Interfaces.Middleware;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (NotFoundException exception)
        {
            logger.LogInformation(exception, "Resource not found on {Path}", context.Request.Path);

            await WriteProblemAsync(context, HttpStatusCode.NotFound, "resource_not_found", "Resource not found", exception.Message);
        }
        catch (DomainException exception)
        {
            logger.LogWarning(exception, "Domain rule violation on {Path}", context.Request.Path);

            await WriteProblemAsync(context, HttpStatusCode.UnprocessableEntity, "domain_rule_violation", "Business rule violation", exception.Message);
        }
        catch (ConflictException exception)
        {
            logger.LogWarning(exception, "State conflict on {Path}", context.Request.Path);
            await WriteProblemAsync(context, HttpStatusCode.Conflict, "state_conflict", "State conflict", exception.Message);
        }
        catch (UnsupportedContentTypeException exception)
        {
            logger.LogWarning(exception, "Unsupported media type on {Path}", context.Request.Path);
            await WriteProblemAsync(context, HttpStatusCode.UnsupportedMediaType, "unsupported_media_type", "Unsupported media type", exception.Message);
        }
        catch (PayloadTooLargeException exception)
        {
            logger.LogWarning(exception, "Payload too large on {Path}", context.Request.Path);
            await WriteProblemAsync(context, HttpStatusCode.RequestEntityTooLarge, "payload_too_large", "Payload too large", exception.Message);
        }
        catch (UnprocessableDocumentException exception)
        {
            logger.LogWarning(exception, "Unprocessable document on {Path}", context.Request.Path);
            await WriteProblemAsync(context, HttpStatusCode.UnprocessableEntity, "unprocessable_document", "Unprocessable document", exception.Message);
        }
        catch (ExternalServiceUnavailableException exception)
        {
            logger.LogError(exception, "External service unavailable on {Path}", context.Request.Path);
            await WriteProblemAsync(context, HttpStatusCode.ServiceUnavailable, "external_service_unavailable", "External service unavailable", exception.Message);
        }
        catch (ArgumentException exception)
        {
            logger.LogWarning(exception, "Invalid argument on {Path}", context.Request.Path);

            await WriteProblemAsync(context, HttpStatusCode.BadRequest, "invalid_request", "Invalid request", exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            logger.LogWarning(exception, "Forbidden operation on {Path}", context.Request.Path);
            await WriteProblemAsync(context, HttpStatusCode.Forbidden, "forbidden", "Forbidden", exception.Message);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            logger.LogWarning(exception, "Concurrency conflict on {Path}", context.Request.Path);
            await WriteProblemAsync(context, HttpStatusCode.Conflict, "concurrency_conflict", "Concurrent modification", "The resource changed while the request was being processed. Reload it and try again.");
        }
        catch (DbUpdateException exception)
        {
            logger.LogWarning(exception, "Persistence conflict on {Path}", context.Request.Path);
            await WriteProblemAsync(context, HttpStatusCode.Conflict, "persistence_conflict", "Persistence conflict", "The operation conflicts with the current persisted state.");
        }
        catch (BadHttpRequestException exception)
        {
            logger.LogWarning(exception, "Malformed HTTP request on {Path}", context.Request.Path);
            var status = Enum.IsDefined(typeof(HttpStatusCode), exception.StatusCode)
                ? (HttpStatusCode)exception.StatusCode
                : HttpStatusCode.BadRequest;
            await WriteProblemAsync(context, status, "invalid_http_request", "Invalid HTTP request", exception.Message);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled error on {Path}", context.Request.Path);
            await WriteProblemAsync(context, HttpStatusCode.InternalServerError, "internal_error", "Unexpected server error", "The server could not complete the request.");
        }
    }

    private static Task WriteProblemAsync(
        HttpContext context,
        HttpStatusCode status,
        string code,
        string title,
        string detail)
    {
        context.Response.StatusCode = (int)status;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Status = (int)status,
            Title = title,
            Detail = detail,
            Type = $"https://smartquote.app/problems/{code}",
            Instance = context.Request.Path
        };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = context.TraceIdentifier;

        return context.Response.WriteAsJsonAsync(problem);
    }
}
