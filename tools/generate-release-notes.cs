// Generate release notes between git tags or HEAD for local testing.
// Usage: dotnet run --file tools/generate-release-notes.cs [FromTag] [ToRef]
// FromTag defaults to the newest tag; ToRef defaults to HEAD.
#:property Nullable=enable
using System.Diagnostics;
using System.Text.RegularExpressions;

string toRef = args.Length > 1 ? args[1] : "HEAD";
string fromTag;
if (args.Length > 0)
{
    fromTag = args[0];
}
else
{
    string tags = await Git("tag --sort=-version:refname");
    fromTag = tags.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .FirstOrDefault() ?? "";
}

string range = string.IsNullOrEmpty(fromTag) ? toRef : $"{fromTag}..{toRef}";
await Console.Error.WriteLineAsync($"Generating release notes for range: {range}");

var groups = new Dictionary<string, List<string>>
{
    ["feat"] = [],
    ["fix"] = [],
    ["perf"] = [],
    ["refactor"] = [],
};

foreach (var subject in (await Git($"log {range} --no-merges --pretty=format:%s")).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
{
    var match = Regex.Match(subject, @"^(feat|fix|perf|refactor)(\([^)]*\))?:\s*(.+)$", RegexOptions.None, TimeSpan.FromMilliseconds(200));
    if (match.Success)
    {
        groups[match.Groups[1].Value].Add(match.Groups[3].Value);
    }
}

await Console.Out.WriteLineAsync("## Release notes");
Console.WriteLine();

WriteSection("### Features", groups["feat"]);
WriteSection("### Bug fixes", groups["fix"]);
WriteSection("### Performance", groups["perf"]);
WriteSection("### Refactoring", groups["refactor"]);

if (groups.Values.All(g => g.Count == 0))
{
    Console.WriteLine("Maintenance updates and general improvements.");
}

static void WriteSection(string title, List<string> items)
{
    if (items.Count == 0)
    {
        return;
    }
    Console.WriteLine(title);
    foreach (var item in items)
    {
        Console.WriteLine($"- {item}");
    }
    Console.WriteLine();
}

static string ResolveGit()
{
    string executable = OperatingSystem.IsWindows() ? "git.exe" : "git";
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

static async Task<string> Git(string arguments)
{
    var start = new ProcessStartInfo(ResolveGit(), arguments)
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
    };
    using var process = Process.Start(start) ?? throw new InvalidOperationException("git could not be started.");
    string output = await process.StandardOutput.ReadToEndAsync();
    await process.StandardError.ReadToEndAsync();
    if (!process.WaitForExit(30_000))
    {
        await Console.Error.WriteLineAsync("git did not exit in time.");
        Environment.Exit(1);
    }
    if (process.ExitCode != 0)
    {
        await Console.Error.WriteLineAsync("git failed. Provide an explicit tag as the first argument.");
        Environment.Exit(process.ExitCode);
    }
    return output;
}
