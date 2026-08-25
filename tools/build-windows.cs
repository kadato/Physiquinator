// Build the .NET MAUI Windows app with the WindowsAppSDK bundled and
// optionally package it as a ZIP. Must run on Windows.
// Usage: dotnet run --file tools/build-windows.cs [--output <path>] [--zip [path]]
#:property Nullable=enable
using System.Diagnostics;
using System.IO.Compression;

string output = "./artifacts/windows";
string? zip = null;
bool dryRun = false;

int i = 0;
while (i < args.Length)
{
    switch (args[i])
    {
        case "--output":
            output = args[++i];
            break;
        case "--zip":
            zip = i + 1 < args.Length && !args[i + 1].StartsWith("--") ? args[++i] : "./Physiquinator-Windows.zip";
            break;
        case "--dry-run":
            dryRun = true;
            break;
        default:
            await Console.Error.WriteLineAsync($"Unknown argument: {args[i]}");
            return 1;
    }
    i++;
}

Console.WriteLine("Building Windows application...");
Console.WriteLine("Includes: WindowsAppSDK runtime bundled");
Console.WriteLine("Requires on the user's machine: .NET 11 Desktop Runtime (x64)");

var start = new ProcessStartInfo(ResolveDotNet())
{
    ArgumentList =
    {
        "publish", "Physiquinator.csproj",
        "-f", "net11.0-windows10.0.19041.0",
        "-c", "Release",
        "-p:WindowsPackageType=None",
        "-p:WindowsAppSDKSelfContained=true",
        "-p:SelfContained=false",
        "-p:PublishTrimmed=false",
        "-o", output,
    },
    UseShellExecute = false,
};

if (dryRun)
{
    Console.WriteLine($"Dry run: {start.FileName} {string.Join(' ', start.ArgumentList)}");
    return 0;
}
using var process = Process.Start(start) ?? throw new InvalidOperationException("dotnet could not be started.");
await process.StandardOutput.ReadToEndAsync();
string errors = await process.StandardError.ReadToEndAsync();
if (errors.Length > 0)
{
    await Console.Error.WriteAsync(errors);
}
if (!process.WaitForExit(600_000))
{
    await Console.Error.WriteLineAsync("dotnet publish did not exit in time.");
    return 1;
}
if (process.ExitCode != 0)
{
    await Console.Error.WriteLineAsync($"Build failed with exit code {process.ExitCode}.");
    return process.ExitCode;
}

string exePath = Path.Combine(output, "Physiquinator.exe");
if (!File.Exists(exePath))
{
    await Console.Error.WriteLineAsync($"Output executable not found at {exePath}");
    return 1;
}

double totalMB = Math.Round(new DirectoryInfo(output).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length) / 1024.0 / 1024.0, 2);
Console.WriteLine($"Build completed. Executable: {exePath} ({Math.Round(new FileInfo(exePath).Length / 1024.0 / 1024.0, 2)} MB), total size {totalMB} MB.");

if (zip is not null)
{
    if (File.Exists(zip))
    {
        File.Delete(zip);
    }
    await ZipFile.CreateFromDirectoryAsync(output, zip, CompressionLevel.Optimal, includeBaseDirectory: false);
    Console.WriteLine($"ZIP created: {Path.GetFullPath(zip)} ({Math.Round(new FileInfo(zip).Length / 1024.0 / 1024.0, 2)} MB).");
}

Console.WriteLine("Run the application from the output folder. Download the .NET 11 Desktop Runtime at https://dotnet.microsoft.com/download/dotnet/11.0");
return 0;

static string ResolveDotNet()
{
    string executable = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
    foreach (string dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
    {
        string candidate = Path.Combine(dir.Trim(), executable);
        if (File.Exists(candidate))
        {
            return candidate;
        }
    }
    return executable;
}
