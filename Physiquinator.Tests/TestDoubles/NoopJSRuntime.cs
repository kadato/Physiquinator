using Microsoft.JSInterop;

namespace Physiquinator.Tests.TestDoubles;

/// <summary>No-op <see cref="IJSRuntime"/> for tests that construct Blazor-adjacent services.</summary>
public sealed class NoopJSRuntime : IJSRuntime
{
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
        new(default(TValue)!);

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
        new(default(TValue)!);
}
