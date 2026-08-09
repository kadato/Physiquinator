namespace Physiquinator.UI.Localization;

/// <summary>
/// Marker type for <c>IStringLocalizer&lt;UiText&gt;</c>. English strings are
/// used as resource keys (falling back to the key itself when no translation
/// exists), so only non-English .resx files need to ship.
/// </summary>
public sealed class UiText
{
    /// <summary>Resource marker type; instantiation is not needed.</summary>
    public UiText()
    {
    }
}
