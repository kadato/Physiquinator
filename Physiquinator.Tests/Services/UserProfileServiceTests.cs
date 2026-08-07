using Physiquinator.Core.Data;
using Physiquinator.Core.Models;
using Physiquinator.Core.Services;
using Physiquinator.Tests.TestDoubles;
using Xunit;

namespace Physiquinator.Tests.Services;

public class UserProfileServiceTests : IAsyncLifetime
{
    private string _dbPath = null!;
    private AppDatabase _db = null!;
    private InMemoryPreferences _preferences = null!;
    private TempDbPathProvider _pathProvider = null!;
    private UserProfileService _service = null!;
    private WorkoutPlanRepository _planRepository = null!;

    static UserProfileServiceTests() => SQLitePCL.Batteries_V2.Init();

    public async Task InitializeAsync()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"physiquinator_profile_test_{Guid.NewGuid():N}.db");
        _db = new AppDatabase(_dbPath);
        await _db.EnsureInitializedAsync();
        _preferences = new InMemoryPreferences();
        _pathProvider = new TempDbPathProvider(_dbPath);
        var session = new WorkoutSessionService(TimeProvider.System);
        _service = new UserProfileService(_db, session, _preferences, _pathProvider, TimeProvider.System);
        _planRepository = new WorkoutPlanRepository(_db);
    }

    public async Task DisposeAsync() => await _db.Database.CloseAsync();

    private Guid CreateProfile(string name)
    {
        _service.CreateProfile(name);
        return _service.GetProfiles().First(p => p.Name == name).Id;
    }

    [Fact]
    public void GetActiveProfile_ReturnsDemoProfileByDefault()
    {
        UserProfile active = _service.GetActiveProfile();

        Assert.Equal(UserProfileService.DemoProfileId, active.Id);
        Assert.Equal("Demo User", active.Name);
        Assert.Single(_service.GetProfiles());
    }

    [Fact]
    public void CreateProfile_AddsProfile_WithoutChangingActiveProfile()
    {
        _service.CreateProfile("Alice");

        Assert.Equal(2, _service.GetProfiles().Count);
        Assert.Equal(UserProfileService.DemoProfileId, _service.GetActiveProfile().Id);
    }

    [Fact]
    public async Task SwitchProfileAsync_HotSwapsDatabase_AndIsolatesData()
    {
        Guid aliceId = CreateProfile("Alice");

        await _service.SwitchProfileAsync(aliceId);

        Assert.Equal(aliceId, _service.GetActiveProfile().Id);
        Assert.Equal(aliceId.ToString(), _preferences.Get(UserProfileService.ActiveProfileIdKey, string.Empty));
        Assert.True(File.Exists(_pathProvider.GetDatabasePath(aliceId)));

        await _planRepository.SavePlanAsync(new WorkoutPlan { Id = Guid.NewGuid(), Name = "Alice Plan" });

        await _service.SwitchProfileAsync(UserProfileService.DemoProfileId);
        Assert.Empty(await _planRepository.GetAllPlansAsync());

        await _planRepository.SavePlanAsync(new WorkoutPlan { Id = Guid.NewGuid(), Name = "Demo Plan" });

        await _service.SwitchProfileAsync(aliceId);
        List<WorkoutPlan> alicePlans = await _planRepository.GetAllPlansAsync();
        Assert.Single(alicePlans);
        Assert.Equal("Alice Plan", alicePlans[0].Name);
    }

    [Fact]
    public async Task SwitchProfileAsync_UnknownProfile_IsNoOp()
    {
        Guid aliceId = CreateProfile("Alice");

        await _service.SwitchProfileAsync(Guid.NewGuid());

        Assert.Equal(UserProfileService.DemoProfileId, _service.GetActiveProfile().Id);
        Assert.NotEqual(aliceId, _service.GetActiveProfile().Id);
    }

    [Fact]
    public async Task DeleteProfileAsync_ActiveProfile_SwitchesToDemoAndDeletesFile()
    {
        Guid aliceId = CreateProfile("Alice");
        await _service.SwitchProfileAsync(aliceId);
        var alicePath = _pathProvider.GetDatabasePath(aliceId);
        Assert.True(File.Exists(alicePath));

        await _service.DeleteProfileAsync(aliceId);

        Assert.False(File.Exists(alicePath));
        Assert.Equal(UserProfileService.DemoProfileId, _service.GetActiveProfile().Id);
        Assert.Single(_service.GetProfiles());
    }

    [Fact]
    public async Task DeleteProfileAsync_InactiveProfile_KeepsActiveProfileAndDeletesFile()
    {
        Guid aliceId = CreateProfile("Alice");
        Guid bobId = CreateProfile("Bob");
        await _service.SwitchProfileAsync(aliceId);
        await _service.SwitchProfileAsync(bobId);
        await _planRepository.SavePlanAsync(new WorkoutPlan { Id = Guid.NewGuid(), Name = "Bob Plan" });
        var alicePath = _pathProvider.GetDatabasePath(aliceId);
        Assert.True(File.Exists(alicePath));

        await _service.DeleteProfileAsync(aliceId);

        Assert.False(File.Exists(alicePath));
        Assert.Equal(bobId, _service.GetActiveProfile().Id);
        List<WorkoutPlan> bobPlans = await _planRepository.GetAllPlansAsync();
        Assert.Single(bobPlans);
        Assert.Equal("Bob Plan", bobPlans[0].Name);
    }

    [Fact]
    public async Task DeleteProfileAsync_DemoProfile_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.DeleteProfileAsync(UserProfileService.DemoProfileId));
    }
}
