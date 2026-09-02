using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OpsDesk.Application.Common.Exceptions;

namespace OpsDesk.Api.ErrorHandling;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(
        ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        int statusCode;
        string title;
        string detail;

        switch (exception)
        {
            case ForbiddenException:
                statusCode = StatusCodes.Status403Forbidden;
                title = "Forbidden";
                detail = exception.Message;
                break;

            case ConflictException:
                statusCode = StatusCodes.Status409Conflict;
                title = "Conflict";
                detail = exception.Message;
                break;

            case UnauthorizedAccessException:
                statusCode = StatusCodes.Status401Unauthorized;
                title = "Unauthorized";
                detail = exception.Message;
                break;

            case ArgumentException:
                statusCode = StatusCodes.Status400BadRequest;
                title = "Bad Request";
                detail = exception.Message;
                break;

            default:
                statusCode = StatusCodes.Status500InternalServerError;
                title = "Internal Server Error";
                detail = "An unexpected error occurred.";
                break;
        }

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(
                exception,
                "An unexpected error occurred.");
        }
        else
        {
            _logger.LogWarning(
                "{ExceptionType}: {Message}",
                exception.GetType().Name,
                exception.Message);
        }

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        problemDetails.Extensions["traceId"] =
            httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = statusCode;

        await httpContext.Response.WriteAsJsonAsync(
            problemDetails,
            options: null,
            contentType: "application/problem+json",
            cancellationToken: cancellationToken);

        return true;
    }
}
