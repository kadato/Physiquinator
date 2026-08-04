namespace Physiquinator.Services;

/// <summary>Theme setup invoked during app initialization (implemented by <see cref="ThemeService"/>).</summary>
public interface IThemeInitialization
{
    Task EnsureInitializedAsync();
}
