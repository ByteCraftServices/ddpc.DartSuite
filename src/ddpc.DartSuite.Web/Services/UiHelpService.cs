using System.Collections.Concurrent;

namespace ddpc.DartSuite.Web.Services;

public interface IUiHelpService
{
    string GetContent(string key, string? fallback = null);
    string GetTooltip(string key, string? fallback = null);
}

public sealed class UiHelpService(IWebHostEnvironment environment, ILogger<UiHelpService> logger) : IUiHelpService
{
    private readonly Lazy<IReadOnlyDictionary<string, string>> _entries = new(() => LoadEntries(environment, logger), true);

    public string GetContent(string key, string? fallback = null)
    {
        if (string.IsNullOrWhiteSpace(key))
            return fallback ?? string.Empty;

        return _entries.Value.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : (fallback ?? string.Empty);
    }

    public string GetTooltip(string key, string? fallback = null)
    {
        var content = GetContent(key, fallback);
        if (string.IsNullOrWhiteSpace(content))
            return string.Empty;

        var firstLine = content
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()?
            .Trim();

        if (string.IsNullOrWhiteSpace(firstLine))
            return string.Empty;

        const int maxLength = 220;
        return firstLine.Length <= maxLength
            ? firstLine
            : firstLine[..maxLength] + "...";
    }

    private static IReadOnlyDictionary<string, string> LoadEntries(IWebHostEnvironment environment, ILogger logger)
    {
        var docsPath = ResolveDocsPath(environment.ContentRootPath);
        if (docsPath is null)
        {
            logger.LogWarning("UiHelpService: docs folder not found. Help content is disabled.");
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var files = Directory
            .GetFiles(docsPath, "*.md", SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var merged = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            try
            {
                var markdown = File.ReadAllText(file);
                var parsed = UiHelpMarkdownParser.Parse(markdown);
                foreach (var (key, value) in parsed)
                {
                    merged.TryAdd(key, value);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "UiHelpService: Failed to parse help file {HelpFile}", file);
            }
        }

        logger.LogInformation("UiHelpService: Loaded {EntryCount} help entries from {DocsPath}", merged.Count, docsPath);
        return new Dictionary<string, string>(merged, StringComparer.OrdinalIgnoreCase);
    }

    private static string? ResolveDocsPath(string contentRootPath)
    {
        var current = new DirectoryInfo(contentRootPath);
        for (var i = 0; i < 8 && current is not null; i++)
        {
            var candidate = Path.Combine(current.FullName, "docs");
            if (Directory.Exists(candidate))
                return candidate;

            current = current.Parent;
        }

        return null;
    }
}
