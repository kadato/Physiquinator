using Microsoft.Extensions.DependencyInjection;

namespace Physiquinator.UI.Services;

/// <summary>
/// UI-layer helpers that keep pages thin and theming consistent.
/// </summary>
public static class UiServiceCollectionExtensions
{
    public static IServiceCollection AddPhysiquinatorUiServices(this IServiceCollection services)
    {
        services.AddScoped<UiFeedbackService>();
        return services;
    }
}
