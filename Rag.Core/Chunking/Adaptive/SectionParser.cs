using System.Text;

namespace Rag.Core.Chunking.Adaptive;

internal sealed class SectionParser
{
    private const int MaxPlainHeadingLength = 80;
    private readonly bool _trimWhitespace;

    public SectionParser(bool trimWhitespace)
    {
        _trimWhitespace = trimWhitespace;
    }

    public IEnumerable<Section> Parse(string text)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        var normalized = text.Replace("\r\n", "\n");
        var lines = normalized.Split('\n');

        var sections = new List<Section>();
        var currentPath = new List<string>();
        var buffer = new StringBuilder();
        var sawHeading = false;

        void Flush()
        {
            if (buffer.Length == 0)
            {
                return;
            }

            sections.Add(new Section(currentPath.ToArray(), buffer.ToString().Trim('\n')));
            buffer.Clear();
        }

        foreach (var raw in lines)
        {
            var line = _trimWhitespace ? raw.Trim() : raw;
            var headingCandidate = line.Trim();

            if (TryParseMarkdownHeading(headingCandidate, out var level, out var heading))
            {
                sawHeading = true;
                Flush();
                UpdatePath(currentPath, level, heading);
                continue;
            }

            if (!sawHeading && TryParsePlainHeading(headingCandidate, out heading, out level))
            {
                Flush();
                UpdatePath(currentPath, level, heading);
                continue;
            }

            buffer.AppendLine(line);
        }

        Flush();

        if (sections.Count == 0)
        {
            sections.Add(new Section(currentPath.ToArray(), string.Empty));
        }

        return sections;
    }

    private static void UpdatePath(List<string> path, int level, string heading)
    {
        level = System.Math.Clamp(level, 1, 6);

        if (path.Count >= level)
        {
            path.RemoveRange(level - 1, path.Count - (level - 1));
        }

        path.Add(heading);
    }

    private static bool TryParseMarkdownHeading(string line, out int level, out string heading)
    {
        level = 0;
        heading = string.Empty;

        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var hashCount = 0;
        while (hashCount < line.Length && line[hashCount] == '#')
        {
            hashCount++;
        }

        if (hashCount is < 1 or > 6)
        {
            return false;
        }

        if (hashCount >= line.Length || line[hashCount] != ' ')
        {
            return false;
        }

        heading = line[(hashCount + 1)..].Trim().TrimEnd(':');
        level = hashCount;
        return heading.Length > 0;
    }

    private static bool TryParsePlainHeading(string line, out string heading, out int level)
    {
        heading = string.Empty;
        level = 1;

        if (string.IsNullOrWhiteSpace(line) || line.EndsWith(".", StringComparison.Ordinal))
        {
            return false;
        }

        if (line.Length > MaxPlainHeadingLength)
        {
            return false;
        }

        if (LooksLikeNumberedHeading(line, out level))
        {
            heading = CleanHeading(line);
            return true;
        }

        if (line.EndsWith(":", StringComparison.Ordinal))
        {
            heading = CleanHeading(line[..^1]);
            level = 1;
            return true;
        }

        if (IsMostlyUpper(line))
        {
            heading = CleanHeading(line);
            level = 1;
            return true;
        }

        return false;
    }

    private static bool LooksLikeNumberedHeading(string line, out int level)
    {
        level = 1;

        var trimmed = line.Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

        if (StartsWithRomanNumeral(trimmed))
        {
            return true;
        }

        if (StartsWithLetterIndex(trimmed))
        {
            return true;
        }

        var depth = 0;
        var currentNumeric = new StringBuilder();
        foreach (var ch in trimmed)
        {
            if (char.IsDigit(ch))
            {
                currentNumeric.Append(ch);
                continue;
            }

            if (ch == '.')
            {
                if (currentNumeric.Length == 0)
                {
                    return false;
                }

                depth++;
                currentNumeric.Clear();
                continue;
            }

            if (ch is ')' or '.' && currentNumeric.Length > 0)
            {
                depth++;
                break;
            }

            break;
        }

        if (depth == 0 && currentNumeric.Length == 0)
        {
            return false;
        }

        level = System.Math.Clamp(depth + 1, 1, 6);
        return true;
    }

    private static bool StartsWithRomanNumeral(string text)
    {
        var trimmed = text.TrimStart();
        var index = 0;

        while (index < trimmed.Length && "IVXLCDM".Contains(char.ToUpperInvariant(trimmed[index])))
        {
            index++;
        }

        if (index == 0 || index >= trimmed.Length)
        {
            return false;
        }

        return trimmed[index] is '.' or ')' && index <= 6;
    }

    private static bool StartsWithLetterIndex(string text)
    {
        var trimmed = text.TrimStart();
        if (trimmed.Length < 2)
        {
            return false;
        }

        return char.IsLetter(trimmed[0]) && (trimmed[1] is '.' or ')');
    }

    private static bool IsMostlyUpper(string line)
    {
        var letters = line.Count(char.IsLetter);
        if (letters < 4)
        {
            return false;
        }

        var upper = line.Count(char.IsUpper);
        return upper >= letters * 0.7;
    }

    private static string CleanHeading(string heading)
    {
        var cleaned = heading.Trim().TrimEnd(':');
        return cleaned;
    }

    internal sealed record Section(IReadOnlyList<string> Path, string Content);
}
