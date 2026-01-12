using System.Text;

namespace Rag.Core.Chunking.AiChunker;

internal sealed class SentenceSplitter
{
    private static readonly char[] Delimiters = { '.', '?', '!' };

    public IReadOnlyList<string> Split(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        var sentences = new List<string>();
        var buffer = new StringBuilder();

        foreach (var ch in text)
        {
            buffer.Append(ch);

            if (Delimiters.Contains(ch))
            {
                Emit();
            }
        }

        Emit();
        return sentences;

        void Emit()
        {
            if (buffer.Length == 0)
            {
                return;
            }

            var candidate = buffer.ToString().Trim();
            if (candidate.Length > 0)
            {
                sentences.Add(candidate);
            }

            buffer.Clear();
        }
    }
}
