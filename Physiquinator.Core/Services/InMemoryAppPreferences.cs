namespace Physiquinator.Core.Services;

/// <summary>
/// Base in-memory <see cref="IAppPreferences"/> with consistent bool handling.
/// Platform implementations can subclass and add persistence (file, localStorage, etc).
/// Keeps Get/Set logic in one place so bool serialization does not drift between Web, Wasm, and test doubles.
/// </summary>
public class InMemoryAppPreferences : IAppPreferences
{
    protected readonly Dictionary<string, string> Values = new(StringComparer.Ordinal);

    public virtual string Get(string key, string defaultValue) =>
        Values.TryGetValue(key, out var value) ? value : defaultValue;

    public virtual bool Get(string key, bool defaultValue)
    {
        if (!Values.TryGetValue(key, out var value))
            return defaultValue;
        return bool.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    public virtual void Set(string key, string value) => Values[key] = value;

    public virtual void Set(string key, bool value) => Values[key] = value.ToString();
}
