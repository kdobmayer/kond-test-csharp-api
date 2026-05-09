namespace DocManager.Services;

public interface IFileStorageService
{
    Task<string> SaveFileAsync(Stream stream, string fileName, string contentType);
    Task<Stream> GetFileAsync(string storagePath);
    Task DeleteFileAsync(string storagePath);
    Task<string> CopyFileAsync(string sourcePath, string destinationFileName);
    string GetStorageDirectory();
}

public class FileStorageService : IFileStorageService
{
    private readonly string _basePath;

    public FileStorageService(IConfiguration configuration)
    {
        _basePath = configuration.GetValue<string>("FileStorage:BasePath")
            ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads");
        Directory.CreateDirectory(_basePath);
    }

    public async Task<string> SaveFileAsync(Stream stream, string fileName, string contentType)
    {
        var uniqueName = $"{Guid.NewGuid():N}_{fileName}";
        var filePath = Path.Combine(_basePath, uniqueName);

        await using var fileStream = new FileStream(filePath, FileMode.Create);
        await stream.CopyToAsync(fileStream);

        return uniqueName;
    }

    public Task<Stream> GetFileAsync(string storagePath)
    {
        var filePath = Path.Combine(_basePath, storagePath);
        if (!File.Exists(filePath))
            throw new FileNotFoundException("File not found in storage", storagePath);

        Stream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        return Task.FromResult(stream);
    }

    public Task DeleteFileAsync(string storagePath)
    {
        var filePath = Path.Combine(_basePath, storagePath);
        if (File.Exists(filePath))
            File.Delete(filePath);
        return Task.CompletedTask;
    }

    public async Task<string> CopyFileAsync(string sourcePath, string destinationFileName)
    {
        var sourceFilePath = Path.Combine(_basePath, sourcePath);
        if (!File.Exists(sourceFilePath))
            throw new FileNotFoundException("Source file not found", sourcePath);

        var uniqueName = $"{Guid.NewGuid():N}_{destinationFileName}";
        var destFilePath = Path.Combine(_basePath, uniqueName);

        await using var source = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read);
        await using var dest = new FileStream(destFilePath, FileMode.Create);
        await source.CopyToAsync(dest);

        return uniqueName;
    }

    public string GetStorageDirectory() => _basePath;
}
