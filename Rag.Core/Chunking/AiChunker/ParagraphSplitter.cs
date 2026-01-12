using System.Text;

namespace Rag.Core.Chunking.AiChunker;

internal sealed class ParagraphSplitter
{
    private const int MaxHeadingLength = 80;

    public IReadOnlyList<Paragraph> Split(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<Paragraph>();
        }

        var normalized = text.Replace("\r\n", "\n");
        var lines = normalized.Split('\n');

        var paragraphs = new List<Paragraph>();
        var buffer = new List<string>();
        var headingStack = new List<string>();
        var index = 0;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                Flush();
                continue;
            }

            buffer.Add(line);
        }

        Flush();
        return paragraphs;

        void Flush()
        {
            if (buffer.Count == 0)
            {
                return;
            }

            var paragraphText = string.Join("\n", buffer).Trim();
            var isHeading = IsHeading(paragraphText);

            if (isHeading)
            {
                UpdateHeadingStack(paragraphText);
            }

            paragraphs.Add(new Paragraph(index, paragraphText, string.Join(" > ", headingStack), isHeading));
            index++;
            buffer.Clear();
        }

        void UpdateHeadingStack(string heading)
        {
            if (headingStack.Count == 0)
            {
                headingStack.Add(heading);
                return;
            }

            headingStack[^1] = heading;
        }
    }

    private static bool IsHeading(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        if (trimmed.Length > MaxHeadingLength)
        {
            return false;
        }

        if (trimmed.StartsWith('#'))
        {
            return true;
        }

        if (trimmed.EndsWith(":", StringComparison.Ordinal))
        {
            return true;
        }

        var letters = trimmed.Count(char.IsLetter);
        if (letters >= 4)
        {
            var upper = trimmed.Count(char.IsUpper);
            if (upper >= letters * 0.7)
            {
                return true;
            }
        }

        return false;
    }
}

public sealed record Paragraph(int Index, string Text, string HeaderPath, bool IsHeading);
