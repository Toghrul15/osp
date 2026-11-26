namespace MyApp.BLL.Services;

public interface ICurrentUserService
{
    bool IsAuthenticated { get; }
    string UserId { get; }
    bool IsInRole(string role);
}
