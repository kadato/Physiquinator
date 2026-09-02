using System.Globalization;
using System.Text.Json;
using Microsoft.JSInterop;
using Physiquinator.Core.Services;

namespace Physiquinator.Wasm.Services;

/// <summary>
/// IAppPreferences backed by browser localStorage. Reads come from an
/// in-memory dictionary hydrated once by <see cref="Initialize"/> (called after
/// host build, before rendering). Writes go to localStorage immediately.
/// Keys are prefixed so only this app's entries are loaded.
/// </summary>
public sealed class WasmAppPreferences : InMemoryAppPreferences
{
    private const string Prefix = "physiquinator.pref.";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private IJSInProcessRuntime? _js;

    /// <summary>Must be called once the host is built, before first use.</summary>
    public void Initialize(IJSRuntime js)
    {
        // WebAssembly can invoke synchronous JavaScript directly. LocalStorage
        // is synchronous, so no promise plumbing is needed here.
        if (js is IJSInProcessRuntime inProcess)
        {
            _js = inProcess;
        }
        try
        {
            var json = Js()?.Invoke<string>(
                "eval",
                "JSON.stringify(Object.fromEntries(Object.entries(localStorage).filter(([k]) => k.startsWith('" + Prefix + "'))))");
            if (string.IsNullOrEmpty(json))
            {
                return;
            }
            var stored = JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions);
            if (stored == null)
            {
                return;
            }
            foreach (var (prefixedKey, value) in stored)
            {
                Values[prefixedKey[Prefix.Length..]] = value ?? string.Empty;
            }
        }
        catch
        {
            // Storage unavailable: fall back to in-memory defaults for this session.
        }
    }

    public override string Get(string key, string defaultValue) =>
        Values.TryGetValue(key, out var value) ? value : defaultValue;

    public override bool Get(string key, bool defaultValue)
    {
        var raw = Get(key, defaultValue ? "true" : "false");
        return bool.TryParse(raw, out var parsed) ? parsed : defaultValue;
    }

    public override void Set(string key, string value)
    {
        Values[key] = value;
        WriteToStorage(key, value);
    }

    public override void Set(string key, bool value) => Set(key, value ? "true" : "false");

    private void WriteToStorage(string key, string value)
    {
        try
        {
            Js()?.Invoke<object?>(
                "eval",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"localStorage.setItem({JsonSerializer.Serialize(Prefix + key, JsonOptions)}, {JsonSerializer.Serialize(value, JsonOptions)})"));
        }
        catch
        {
            // Ignore write failures. The in-memory copy keeps this session working.
        }
    }

    private IJSInProcessRuntime? Js() => _js;
}
