using System.Text;

namespace Rag.Core.Chunking.Adaptive;

internal sealed class SentenceSplitter
{
    private static readonly char[] Delimiters = { '.', '?', '!' };

    public IEnumerable<string> Split(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        var buffer = new StringBuilder();

        foreach (var ch in text)
        {
            buffer.Append(ch);

            if (Delimiters.Contains(ch))
            {
                EmitIfAny(buffer, out var sentence);
                if (sentence is not null)
                {
                    yield return sentence;
                }
            }
        }

        EmitIfAny(buffer, out var last);
        if (last is not null)
        {
            yield return last;
        }
    }

    private static void EmitIfAny(StringBuilder buffer, out string? sentence)
    {
        sentence = null;

        if (buffer.Length == 0)
        {
            return;
        }

        var candidate = buffer.ToString().Trim();
        if (candidate.Length > 0)
        {
            sentence = candidate;
        }

        buffer.Clear();
    }
}
