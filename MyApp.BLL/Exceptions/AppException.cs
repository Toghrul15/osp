namespace MyApp.BLL.Exceptions;

public abstract class AppException(string message, int statusCode, IReadOnlyList<string>? errors = null)
    : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public IReadOnlyList<string> Errors { get; } = errors ?? [];
}

public class BadRequestException(string message, IReadOnlyList<string>? errors = null)
    : AppException(message, 400, errors);

public class UnauthorizedException(string message) : AppException(message, 401);

public class ForbiddenException(string message) : AppException(message, 403);

public class NotFoundException(string message) : AppException(message, 404);

public class ConflictException(string message) : AppException(message, 409);
