using SQLite;
using System.Security.Cryptography;

namespace Physiquinator.Web.Services;

/// <summary>An authenticated account on the web host.</summary>
public sealed record WebUser(string Id, string Username);

/// <summary>
/// Account store for the web host: a small SQLite database with PBKDF2-hashed passwords.
/// Seeds a demo account (username/password from AUTH_DEMO_USERNAME / AUTH_DEMO_PASSWORD,
/// defaulting to demo / demo1234) so the portfolio instance is immediately usable.
/// </summary>
public sealed class WebUserStore
{
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    private readonly SQLiteAsyncConnection _database;
    private readonly Task _initializationTask;

    public WebUserStore(string dbPath)
    {
        _database = new SQLiteAsyncConnection(dbPath);
        _initializationTask = InitializeAsync();
    }

    public async Task<WebUser?> FindByUsernameAsync(string username)
    {
        await EnsureInitializedAsync();
        WebUserEntity? entity = await _database.Table<WebUserEntity>()
            .Where(user => user.Username == username)
            .FirstOrDefaultAsync();
        return entity is null ? null : new WebUser(entity.Id, entity.Username);
    }

    /// <summary>Creates the account and returns true, or false when the username is taken.</summary>
    public async Task<bool> TryCreateAsync(string username, string password)
    {
        await EnsureInitializedAsync();

        if (await _database.Table<WebUserEntity>()
                .Where(user => user.Username == username)
                .CountAsync() > 0)
            return false;

        await _database.InsertAsync(new WebUserEntity
        {
            Id = Guid.NewGuid().ToString(),
            Username = username,
            PasswordHash = HashPassword(password)
        });
        return true;
    }

    public async Task<WebUser?> ValidateCredentialsAsync(string username, string password)
    {
        await EnsureInitializedAsync();
        WebUserEntity? entity = await _database.Table<WebUserEntity>()
            .Where(user => user.Username == username)
            .FirstOrDefaultAsync();

        if (entity is null || !VerifyPassword(password, entity.PasswordHash))
            return null;

        return new WebUser(entity.Id, entity.Username);
    }

    /// <summary>The seeded demo account login (AUTH_DEMO_USERNAME, default "demo").</summary>
    public string DemoUsername { get; private set; } = "demo";

    private async Task EnsureInitializedAsync() => await _initializationTask;

    private async Task InitializeAsync()
    {
        await _database.CreateTableAsync<WebUserEntity>();

        var demoUsername = Environment.GetEnvironmentVariable("AUTH_DEMO_USERNAME") ?? "demo";
        var demoPassword = Environment.GetEnvironmentVariable("AUTH_DEMO_PASSWORD") ?? "demo1234";
        DemoUsername = demoUsername;

        if (await _database.Table<WebUserEntity>()
                .Where(user => user.Username == demoUsername)
                .CountAsync() == 0)
        {
            await _database.InsertAsync(new WebUserEntity
            {
                Id = Guid.NewGuid().ToString(),
                Username = demoUsername,
                PasswordHash = HashPassword(demoPassword)
            });
        }
    }

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPassword(string password, string stored)
    {
        var parts = stored.Split('.');
        if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations))
            return false;

        try
        {
            var salt = Convert.FromBase64String(parts[1]);
            var expected = Convert.FromBase64String(parts[2]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    [Table("Users")]
    private sealed class WebUserEntity
    {
        [PrimaryKey]
        public string Id { get; set; } = string.Empty;

        [Unique]
        public string Username { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;
    }
}
