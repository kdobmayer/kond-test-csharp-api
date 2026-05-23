using DocManager.Data;

namespace DocManager.Services;

public enum AuthorizationResult { Allowed, NotFound, Forbidden }

public interface IDocAuthorizationService
{
    Task<AuthorizationResult> CanUseUserAsync(int userId);
    Task<AuthorizationResult> CanManageOwnedResourceAsync(int userId, int ownerUserId);
}

public class DocAuthorizationService : IDocAuthorizationService
{
    private readonly AppDbContext _db;

    public DocAuthorizationService(AppDbContext db)
    {
        _db = db;
    }

    public Task<AuthorizationResult> CanUseUserAsync(int userId) => CheckUserAsync(userId);

    public async Task<AuthorizationResult> CanManageOwnedResourceAsync(int userId, int ownerUserId)
    {
        var userCheck = await CheckUserAsync(userId);
        if (userCheck != AuthorizationResult.Allowed)
            return userCheck;

        return ownerUserId == userId ? AuthorizationResult.Allowed : AuthorizationResult.Forbidden;
    }

    private async Task<AuthorizationResult> CheckUserAsync(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return AuthorizationResult.NotFound;
        if (!user.IsActive) return AuthorizationResult.Forbidden;
        return AuthorizationResult.Allowed;
    }
}
