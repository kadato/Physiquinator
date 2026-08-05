using Physiquinator.Core.Services;

namespace Physiquinator.Web.Services;

/// <summary>In-memory preferences for the browser debug host (reset on refresh).</summary>
public sealed class WebAppPreferences : IAppPreferences
{
    private readonly Dictionary<string, string> _values = [];

    public string Get(string key, string defaultValue) =>
        _values.TryGetValue(key, out var value) ? value : defaultValue;

    public bool Get(string key, bool defaultValue)
    {
        if (!_values.TryGetValue(key, out var value))
            return defaultValue;

        return bool.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    public void Set(string key, string value) => _values[key] = value;

    public void Set(string key, bool value) => _values[key] = value.ToString();
}
