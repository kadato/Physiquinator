using Microsoft.JSInterop;
using Physiquinator.Core.Services;
using Xunit;

namespace Physiquinator.Tests.Services;

/// <summary>
/// Guards the workout rest-timer interop lifecycle: page teardown must reach
/// the JS module (stopRestTimer, unregister handlers) and the scoped instance
/// must keep working when the workout page mounts again after a navigation.
/// Regression: DisposeAsync set a guard before its own JS calls, so every
/// teardown call was swallowed and the JS timer chain survived navigation,
/// ticking a disposed ref and freezing the countdown on return.
/// </summary>
public class WorkoutTimerInteropTests
{
    private sealed class FakeModule : IJSObjectReference
    {
        public List<string> Calls { get; } = [];

        public bool Disposed { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            Calls.Add(identifier);
            return new(default(TValue)!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            Calls.Add(identifier);
            return new(default(TValue)!);
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeJSRuntime(IJSObjectReference module) : IJSRuntime
    {
        public List<string> Imports { get; } = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            Imports.Add(identifier);
            return new((TValue)(object)module);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            Imports.Add(identifier);
            return new((TValue)(object)module);
        }
    }

    private static WorkoutTimerInterop CreateInterop(FakeJSRuntime runtime) => new(runtime);

    [Fact]
    public async Task DisposeAsync_reaches_the_module_and_stops_the_timer()
    {
        var module = new FakeModule();
        var interop = CreateInterop(new FakeJSRuntime(module));

        await interop.InitializeAsync();
        await interop.DisposeAsync();

        Assert.Contains("stopRestTimer", module.Calls);
        Assert.Contains("unregisterUndoKeyHandler", module.Calls);
        Assert.Contains("unregisterBackHandler", module.Calls);
        Assert.Contains("setKeepScreenOn", module.Calls);
    }

    [Fact]
    public async Task Instance_survives_dispose_and_starts_the_timer_again_on_the_next_mount()
    {
        var module = new FakeModule();
        var runtime = new FakeJSRuntime(module);
        var interop = CreateInterop(runtime);
        var firstRef = DotNetObjectReference.Create(new object());
        var secondRef = DotNetObjectReference.Create(new object());

        // First workout page mount.
        await interop.InitializeAsync();
        await interop.StartTimerAsync(firstRef, 1000, 90_000, 90_000, continueMode: false);

        // Navigation away disposes the page-held instance...
        await interop.DisposeAsync();

        // ...and the same scoped instance is re-armed on the return trip.
        await interop.InitializeAsync();
        await interop.StartTimerAsync(secondRef, 1000, 45_000, 90_000, continueMode: false);

        Assert.Single(runtime.Imports);
        Assert.False(module.Disposed);
        Assert.Equal(2, module.Calls.Count(call => call == "startRestTimer"));

        firstRef.Dispose();
        secondRef.Dispose();
    }

    [Fact]
    public async Task Reinitialized_instance_keeps_all_timer_operations_working()
    {
        var module = new FakeModule();
        var interop = CreateInterop(new FakeJSRuntime(module));
        var dotNetRef = DotNetObjectReference.Create(new object());

        await interop.InitializeAsync();
        await interop.DisposeAsync();
        await interop.InitializeAsync();

        await interop.StartTimerAsync(dotNetRef, 1000, 30_000, 30_000, continueMode: false);
        await interop.StopTimerAsync();
        await interop.UnlockAudioAsync();

        Assert.Contains("startRestTimer", module.Calls);
        Assert.Contains("stopRestTimer", module.Calls);
        Assert.Contains("unlockAudioContext", module.Calls);

        dotNetRef.Dispose();
    }

    [Fact]
    public async Task DisposeAsync_is_safe_to_call_twice_like_page_and_container_teardown()
    {
        var module = new FakeModule();
        var interop = CreateInterop(new FakeJSRuntime(module));

        await interop.InitializeAsync();
        await interop.DisposeAsync();
        await interop.DisposeAsync();

        Assert.False(module.Disposed);
        Assert.Contains("stopRestTimer", module.Calls);
    }
}
