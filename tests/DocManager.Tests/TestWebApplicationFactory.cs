using DocManager.Data;
using DocManager.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocManager.Tests;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"TestDb_{Guid.NewGuid():N}";
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"docmanager_test_{Guid.NewGuid():N}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_tempDir);

        builder.ConfigureServices(services =>
        {
            // Remove all EF Core registrations
            var descriptorsToRemove = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                    || d.ServiceType == typeof(DbContextOptions)
                    || (d.ServiceType.FullName?.Contains("EntityFrameworkCore") == true
                        && d.ServiceType.FullName?.Contains("InMemory") == false))
                .ToList();

            foreach (var descriptor in descriptorsToRemove)
                services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));

            // Replace file storage with temp directory
            var fsDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IFileStorageService));
            if (fsDescriptor != null)
                services.Remove(fsDescriptor);

            services.AddScoped<IFileStorageService>(_ => new TestFileStorageService(_tempDir));
        });

        builder.UseEnvironment("Testing");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }
}

public class TestFileStorageService : IFileStorageService
{
    private readonly string _basePath;

    public TestFileStorageService(string basePath)
    {
        _basePath = basePath;
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
            throw new FileNotFoundException("File not found", storagePath);
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
