using Physiquinator.Core.Services;

namespace Physiquinator.Tests.TestDoubles;

/// <summary>In-memory <see cref="IDemoSeedPreferences"/> backed by a dictionary.</summary>
public sealed class MemoryDemoSeedPreferences : IDemoSeedPreferences
{
    private readonly Dictionary<string, bool> _values = [];

    public bool Get(string key, bool defaultValue) =>
        _values.TryGetValue(key, out var value) ? value : defaultValue;

    public void Set(string key, bool value) => _values[key] = value;

    public void Clear() => _values.Clear();

    public bool IsDefaultProfile { get; set; } = true;
}
