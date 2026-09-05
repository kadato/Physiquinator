using System.Collections.Concurrent;
using Physiquinator.Core.Services;

namespace Physiquinator.Tests.TestDoubles;

/// <summary>In-memory <see cref="IAppPreferences"/> backed by a dictionary.</summary>
public sealed class InMemoryPreferences : IAppPreferences
{
    private readonly ConcurrentDictionary<string, string> _values = new();

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
