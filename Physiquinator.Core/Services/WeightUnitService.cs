using Physiquinator.Core.Formatting;

namespace Physiquinator.Core.Services;

/// <summary>
/// Display unit for weights (kg or lb), stored per profile. Database storage
/// stays in kilograms; the UI converts on display.
/// </summary>
public sealed class WeightUnitService(IAppPreferences preferences, UserProfileService profiles)
{
    private readonly IAppPreferences _preferences = preferences;
    private readonly UserProfileService _profiles = profiles;

    private string Suffix => $"_{_profiles.GetActiveProfile().Id}";

    public WeightUnit Current
    {
        get
        {
            var raw = _preferences.Get(
                PreferenceKeys.WeightUnitPreference + Suffix,
                WeightUnit.Kilograms.ToString());
            return Enum.TryParse<WeightUnit>(raw, out var unit) ? unit : WeightUnit.Kilograms;
        }
    }

    public void Set(WeightUnit unit)
    {
        _preferences.Set(PreferenceKeys.WeightUnitPreference + Suffix, unit.ToString());
    }
}
