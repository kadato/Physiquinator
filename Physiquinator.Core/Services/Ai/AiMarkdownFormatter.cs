using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Physiquinator.Core.Services.Ai;

/// <summary>
/// Lightweight, XSS-safe Markdown to HTML formatter for AI chat messages.
/// Encode-first, so streaming re-renders stay cheap and safe on phones.
/// </summary>
public static partial class AiMarkdownFormatter
{
    public static string ToHtml(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return string.Empty;

        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var sb = new StringBuilder();

        var inCodeBlock = false;
        var inList = false;
        var isOrderedList = false;

        var li = 0;
        while (li < lines.Length)
        {
            var line = lines[li];
            var trimmed = line.TrimStart();

            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                if (inList)
                {
                    sb.Append(isOrderedList ? "</ol>" : "</ul>");
                    inList = false;
                }

                if (!inCodeBlock)
                {
                    inCodeBlock = true;
                    var lang = trimmed[3..].Trim();
                    var langClass = string.IsNullOrWhiteSpace(lang) ? "" : $" class=\"language-{WebUtility.HtmlEncode(lang)}\"";
                    sb.Append(CultureInfo.InvariantCulture, $"<pre class=\"ai-code-block\"><code{langClass}>");
                }
                else
                {
                    inCodeBlock = false;
                    sb.Append("</code></pre>");
                }
                li++;
                continue;
            }

            if (inCodeBlock)
            {
                sb.AppendLine(WebUtility.HtmlEncode(line));
                li++;
                continue;
            }

            if (IsTableRow(trimmed) && li + 1 < lines.Length && IsTableSeparator(lines[li + 1]))
            {
                if (inList)
                {
                    sb.Append(isOrderedList ? "</ol>" : "</ul>");
                    inList = false;
                }

                var headerCells = SplitTableRow(trimmed);
                li++;
                sb.Append("<div class=\"ai-md-table-wrap\"><table class=\"ai-md-table\"><thead><tr>");
                foreach (var cell in headerCells)
                    sb.Append(CultureInfo.InvariantCulture, $"<th>{FormatInline(cell)}</th>");
                sb.Append("</tr></thead><tbody>");
                while (li + 1 < lines.Length && IsTableRow(lines[li + 1].TrimStart()))
                {
                    li++;
                    sb.Append("<tr>");
                    foreach (var cell in SplitTableRow(lines[li].TrimStart()))
                        sb.Append(CultureInfo.InvariantCulture, $"<td>{FormatInline(cell)}</td>");
                    sb.Append("</tr>");
                }

                sb.Append("</tbody></table></div>");
                li++;
                continue;
            }

            if (IsHorizontalRule(trimmed))
            {
                if (inList)
                {
                    sb.Append(isOrderedList ? "</ol>" : "</ul>");
                    inList = false;
                }
                sb.Append("<hr class=\"ai-md-hr\" />");
                li++;
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                if (inList)
                {
                    sb.Append(isOrderedList ? "</ol>" : "</ul>");
                    inList = false;
                }
                sb.Append("<div class=\"ai-md-spacer\"></div>");
                li++;
                continue;
            }

            if (trimmed.StartsWith("- [ ] ", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("* [ ] ", StringComparison.OrdinalIgnoreCase))
            {
                if (inList)
                {
                    sb.Append(isOrderedList ? "</ol>" : "</ul>");
                    inList = false;
                }
                var itemText = FormatInline(trimmed[6..]);
                sb.Append(CultureInfo.InvariantCulture, $"<div class=\"ai-checklist-item\"><span class=\"ai-check-icon\" aria-hidden=\"true\">☐</span> <span>{itemText}</span></div>");
                li++;
                continue;
            }

            if (trimmed.StartsWith("- [x] ", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("* [x] ", StringComparison.OrdinalIgnoreCase))
            {
                if (inList)
                {
                    sb.Append(isOrderedList ? "</ol>" : "</ul>");
                    inList = false;
                }
                var itemText = FormatInline(trimmed[6..]);
                sb.Append(CultureInfo.InvariantCulture, $"<div class=\"ai-checklist-item ai-checklist-item--done\"><span class=\"ai-check-icon ai-check-icon--checked\" aria-hidden=\"true\">☑</span> <span class=\"ai-checked-text\">{itemText}</span></div>");
                li++;
                continue;
            }

            if (trimmed.StartsWith("### ", StringComparison.Ordinal))
            {
                if (inList) { sb.Append(isOrderedList ? "</ol>" : "</ul>"); inList = false; }
                sb.Append(CultureInfo.InvariantCulture, $"<h5 class=\"ai-md-h5\">{FormatInline(trimmed[4..])}</h5>");
                li++;
                continue;
            }
            if (trimmed.StartsWith("## ", StringComparison.Ordinal))
            {
                if (inList) { sb.Append(isOrderedList ? "</ol>" : "</ul>"); inList = false; }
                sb.Append(CultureInfo.InvariantCulture, $"<h4 class=\"ai-md-h4\">{FormatInline(trimmed[3..])}</h4>");
                li++;
                continue;
            }
            if (trimmed.StartsWith("# ", StringComparison.Ordinal))
            {
                if (inList) { sb.Append(isOrderedList ? "</ol>" : "</ul>"); inList = false; }
                sb.Append(CultureInfo.InvariantCulture, $"<h3 class=\"ai-md-h3\">{FormatInline(trimmed[2..])}</h3>");
                li++;
                continue;
            }

            if (trimmed.StartsWith("> ", StringComparison.Ordinal))
            {
                if (inList) { sb.Append(isOrderedList ? "</ol>" : "</ul>"); inList = false; }
                sb.Append(CultureInfo.InvariantCulture, $"<blockquote class=\"ai-md-quote\">{FormatInline(trimmed[2..])}</blockquote>");
                li++;
                continue;
            }

            if (trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed.StartsWith("* ", StringComparison.Ordinal))
            {
                if (!inList || isOrderedList)
                {
                    if (inList) sb.Append("</ol>");
                    sb.Append("<ul class=\"ai-md-list\">");
                    inList = true;
                    isOrderedList = false;
                }
                sb.Append(CultureInfo.InvariantCulture, $"<li>{FormatInline(trimmed[2..])}</li>");
                li++;
                continue;
            }

            var orderedMatch = OrderedListRegex().Match(trimmed);
            if (orderedMatch.Success)
            {
                if (!inList || !isOrderedList)
                {
                    if (inList) sb.Append("</ul>");
                    sb.Append("<ol class=\"ai-md-list\">");
                    inList = true;
                    isOrderedList = true;
                }
                var content = trimmed[orderedMatch.Length..];
                sb.Append(CultureInfo.InvariantCulture, $"<li>{FormatInline(content)}</li>");
                li++;
                continue;
            }

            if (inList)
            {
                sb.Append(isOrderedList ? "</ol>" : "</ul>");
                inList = false;
            }

            sb.Append(CultureInfo.InvariantCulture, $"<p class=\"ai-md-p\">{FormatInline(line)}</p>");
            li++;
        }

        if (inList) sb.Append(isOrderedList ? "</ol>" : "</ul>");
        if (inCodeBlock) sb.Append("</code></pre>");

        return sb.ToString();
    }

    private static bool IsHorizontalRule(string trimmed) =>
        trimmed is "---" or "***" or "___" ||
        (trimmed.Length >= 3 && trimmed.Trim('-', '*', '_', ' ').Length == 0 && trimmed.Contains('-', StringComparison.Ordinal));

    private static bool IsTableRow(string trimmed) =>
        trimmed.StartsWith('|') && trimmed.EndsWith('|') && trimmed.Contains('|', StringComparison.Ordinal);

    private static bool IsTableSeparator(string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return false;
        var t = line.Trim();
        if (!t.Contains('|') || !t.Contains('-')) return false;
        return t.Trim('|', '-', ':', ' ', '\t').Length == 0;
    }

    private static List<string> SplitTableRow(string row)
    {
        var t = row.Trim();
        if (t.StartsWith('|')) t = t[1..];
        if (t.EndsWith('|')) t = t[..^1];
        return t.Split('|').Select(c => c.Trim()).ToList();
    }

    private static string FormatInline(string text)
    {
        var encoded = WebUtility.HtmlEncode(text);
        var codePlaceholders = new List<string>();
        encoded = InlineCodeRegex().Replace(encoded, m =>
        {
            codePlaceholders.Add(m.Groups[1].Value);
            return $"\u0000CODE{codePlaceholders.Count - 1}\u0000";
        });
        encoded = LinkRegex().Replace(encoded, m => BuildLink(m.Groups[1].Value, m.Groups[2].Value));
        encoded = AutolinkRegex().Replace(encoded, m => BuildLink(m.Value, m.Value));
        encoded = BoldRegex().Replace(encoded, "<strong>$1</strong>");
        encoded = ItalicRegex().Replace(encoded, "<em>$1</em>");
        encoded = StrikeRegex().Replace(encoded, "<del>$1</del>");
        for (var i = 0; i < codePlaceholders.Count; i++)
            encoded = encoded.Replace($"\u0000CODE{i}\u0000", $"<code class=\"ai-inline-code\">{codePlaceholders[i]}</code>", StringComparison.Ordinal);
        return encoded;
    }

    private static string BuildLink(string text, string url)
    {
        var href = url.Trim();
        if (!IsSafeUrl(href))
            return WebUtility.HtmlEncode($"[{WebUtility.HtmlDecode(text)}]({url})");
        var safeHref = WebUtility.HtmlEncode(href);
        return $"<a class=\"ai-md-link\" href=\"{safeHref}\" target=\"_blank\" rel=\"noopener\">{text}</a>";
    }

    private static bool IsSafeUrl(string url) =>
        url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
        url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
        (url.StartsWith('/') && !url.StartsWith("//", StringComparison.Ordinal));

    [GeneratedRegex(@"^\d+\.\s+")]
    private static partial Regex OrderedListRegex();
    [GeneratedRegex(@"`([^`]+)`")]
    private static partial Regex InlineCodeRegex();
    [GeneratedRegex(@"\*\*([^*]+)\*\*")]
    private static partial Regex BoldRegex();
    [GeneratedRegex(@"(?<!\*)\*([^*]+)\*(?!\*)")]
    private static partial Regex ItalicRegex();
    [GeneratedRegex(@"~~([^~]+)~~")]
    private static partial Regex StrikeRegex();
    [GeneratedRegex(@"\[([^\]]+)\]\(([^)\s]+)\)")]
    private static partial Regex LinkRegex();
    [GeneratedRegex(@"(?<![""'=])(https?://[^\s<]+)")]
    private static partial Regex AutolinkRegex();
}
