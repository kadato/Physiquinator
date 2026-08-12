using Physiquinator.Core.Services;
using Xunit;

namespace Physiquinator.Tests.Services;

public class AppUpdateSettingsServiceTests
{
    [Fact]
    public void AutoCheckEnabled_DefaultsToTrue()
    {
        var prefs = new FakeAppPreferences();
        var sut = new AppUpdateSettingsService(prefs);

        Assert.True(sut.AutoCheckEnabled);
    }

    [Fact]
    public void SetAutoCheckEnabled_UpdatesPreferenceValue()
    {
        var prefs = new FakeAppPreferences();
        var sut = new AppUpdateSettingsService(prefs);

        sut.SetAutoCheckEnabled(false);
        Assert.False(sut.AutoCheckEnabled);

        sut.SetAutoCheckEnabled(true);
        Assert.True(sut.AutoCheckEnabled);
    }

    private sealed class FakeAppPreferences : IAppPreferences
    {
        private readonly Dictionary<string, object> _store = [];

        public string Get(string key, string defaultValue)
        {
            if (_store.TryGetValue(key, out var val) && val is string strVal)
            {
                return strVal;
            }
            return defaultValue;
        }

        public bool Get(string key, bool defaultValue)
        {
            if (_store.TryGetValue(key, out var val) && val is bool boolVal)
            {
                return boolVal;
            }
            return defaultValue;
        }

        public void Set(string key, string value)
        {
            _store[key] = value;
        }

        public void Set(string key, bool value)
        {
            _store[key] = value;
        }
    }
}
