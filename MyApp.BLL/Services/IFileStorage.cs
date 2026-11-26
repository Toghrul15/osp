namespace MyApp.BLL.Services;

public interface IFileStorage
{
    Task<string> SaveAsync(Stream content, string extension, string folder, CancellationToken cancellationToken = default);
    Task DeleteAsync(string url, CancellationToken cancellationToken = default);
}
