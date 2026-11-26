using MyApp.BLL.Services;

namespace MyApp.Api.Services;

public class LocalFileStorage(IWebHostEnvironment environment) : IFileStorage
{
    private const string UploadsFolder = "uploads";

    private string UploadsRoot => Path.GetFullPath(Path.Combine(WebRoot, UploadsFolder));

    private string WebRoot => string.IsNullOrEmpty(environment.WebRootPath)
        ? Path.Combine(environment.ContentRootPath, "wwwroot")
        : environment.WebRootPath;

    public async Task<string> SaveAsync(Stream content, string extension, string folder, CancellationToken cancellationToken = default)
    {
        var directory = Path.Combine(UploadsRoot, folder);
        Directory.CreateDirectory(directory);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(directory, fileName);

        await using (var file = File.Create(fullPath))
        {
            await content.CopyToAsync(file, cancellationToken);
        }

        return $"/{UploadsFolder}/{folder}/{fileName}";
    }

    public Task DeleteAsync(string url, CancellationToken cancellationToken = default)
    {
        var relative = url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(WebRoot, relative));

        if (fullPath.StartsWith(UploadsRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }
}
