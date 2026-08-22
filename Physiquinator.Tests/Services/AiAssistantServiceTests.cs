using Physiquinator.Core.Data;
using Physiquinator.Core.Models;
using Physiquinator.Core.Services;
using Physiquinator.Core.Services.Ai;
using Physiquinator.Tests.TestDoubles;
using Xunit;

namespace Physiquinator.Tests.Services;

public class AiAssistantServiceTests
{
    private static AiAssistantService CreateAssistant(
        IAppPreferences prefs,
        UserProfileService profileService,
        TimeProvider time)
    {
        var client = new OpenAiCompatibleClient(new HttpClient());
        var registry = new AiToolRegistry([]);
        return new AiAssistantService(prefs, profileService, client, registry, time);
    }

    private static UserProfileService CreateProfileService(IAppPreferences prefs, string dbPath)
    {
        var db = new Physiquinator.Core.Data.AppDatabase(dbPath);
        return new UserProfileService(
            db,
            new WorkoutSessionService(TimeProvider.System),
            prefs,
            new FixedPathDatabasePathProvider(dbPath),
            TimeProvider.System);
    }

    private sealed class FixedPathDatabasePathProvider(string path) : IDatabasePathProvider
    {
        public string GetDatabasePath(Guid profileId) => path;
    }

    [Fact]
    public async Task Unconfigured_assistant_reports_error_and_persists_conversation()
    {
        var prefs = new InMemoryPreferences();
        var dbPath = Path.Combine(Path.GetTempPath(), $"physiquinator_ai_svc_test_{Guid.NewGuid():N}.db");
        var profileService = CreateProfileService(prefs, dbPath);
        var assistant = CreateAssistant(prefs, profileService, TimeProvider.System);

        prefs.Set(PreferenceKeys.AiEnabled, true);
        prefs.Set(PreferenceKeys.AiApiKey, string.Empty);

        await assistant.SendUserMessageAsync("Hello");

        var messages = assistant.Messages.ToList();
        Assert.Equal(2, messages.Count);
        Assert.Equal(AiMessageRole.User, messages[0].Role);
        Assert.True(messages[1].IsError);

        // The conversation is persisted under the active profile's key.
        var stored = prefs.Get(PreferenceKeys.AiChatHistory, string.Empty);
        Assert.False(string.IsNullOrWhiteSpace(stored));

        // A fresh instance restores the same conversation.
        var restored = CreateAssistant(prefs, profileService, TimeProvider.System);
        Assert.Equal(2, restored.Messages.Count);
        Assert.Equal("Hello", restored.Messages[0].Content);
    }

    [Fact]
    public async Task ClearHistory_removes_persisted_conversation()
    {
        var prefs = new InMemoryPreferences();
        var dbPath = Path.Combine(Path.GetTempPath(), $"physiquinator_ai_svc_test_{Guid.NewGuid():N}.db");
        var profileService = CreateProfileService(prefs, dbPath);
        var assistant = CreateAssistant(prefs, profileService, TimeProvider.System);

        prefs.Set(PreferenceKeys.AiEnabled, true);
        prefs.Set(PreferenceKeys.AiApiKey, string.Empty);
        await assistant.SendUserMessageAsync("Hello");

        assistant.ClearHistory();

        Assert.Empty(assistant.Messages);
        Assert.Equal(string.Empty, prefs.Get(PreferenceKeys.AiChatHistory, "sentinel"));
    }

    [Fact]
    public async Task Chat_history_is_isolated_per_profile()
    {
        var prefs = new InMemoryPreferences();
        var dbPath = Path.Combine(Path.GetTempPath(), $"physiquinator_ai_svc_test_{Guid.NewGuid():N}.db");
        var profileService = CreateProfileService(prefs, dbPath);
        var assistant = CreateAssistant(prefs, profileService, TimeProvider.System);

        prefs.Set(PreferenceKeys.AiEnabled, true);
        prefs.Set(PreferenceKeys.AiApiKey, string.Empty);
        await assistant.SendUserMessageAsync("Message for demo profile");

        // Create a second profile and switch to it. Its history starts empty.
        profileService.CreateProfile("Second");
        var secondProfileId = profileService.GetProfiles().First(p => p.Name == "Second").Id;
        await profileService.SwitchProfileAsync(secondProfileId);

        var secondAssistant = CreateAssistant(prefs, profileService, TimeProvider.System);
        Assert.Empty(secondAssistant.Messages);
    }
}
