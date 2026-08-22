namespace Physiquinator.Core.Services;

/// <summary>
/// Abstraction over persisted key/value preferences so the core layer stays
/// platform-independent and testable. Production implementation backs onto
/// MAUI <c>Preferences</c>. The screenshot tooling backs onto a JSON file.
/// </summary>
public interface IAppPreferences
{
    string Get(string key, string defaultValue);

    bool Get(string key, bool defaultValue);

    void Set(string key, string value);

    void Set(string key, bool value);
}
