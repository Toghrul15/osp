using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MyApp.BLL.Exceptions;

namespace MyApp.Api.Middleware;

public class AppExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<AppExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = new ProblemDetails();

        if (exception is AppException appException)
        {
            problem.Status = appException.StatusCode;
            problem.Title = appException.Message;
            if (appException.Errors.Count > 0)
                problem.Extensions["errors"] = appException.Errors;
        }
        else
        {
            logger.LogError(exception, "Unhandled exception while processing {Path}", httpContext.Request.Path);
            problem.Status = StatusCodes.Status500InternalServerError;
            problem.Title = "An unexpected error occurred.";
        }

        httpContext.Response.StatusCode = problem.Status.Value;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception
        });
    }
}
