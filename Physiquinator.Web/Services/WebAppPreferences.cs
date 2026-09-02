using Physiquinator.Core.Services;

namespace Physiquinator.Web.Services;

/// <summary>In-memory preferences for the browser debug host (reset on refresh). Uses base InMemoryAppPreferences so bool handling stays consistent with Wasm and MAUI.</summary>
#pragma warning disable S2094 // Empty class is intentional: it inherits the full in-memory implementation.
public sealed class WebAppPreferences : InMemoryAppPreferences
{
}
#pragma warning restore S2094
