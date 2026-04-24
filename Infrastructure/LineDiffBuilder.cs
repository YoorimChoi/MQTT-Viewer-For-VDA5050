namespace MqttViewer.Infrastructure;

public sealed class LineDiffResult
{
    public string Text { get; init; } = string.Empty;

    public int Added { get; init; }

    public int Removed { get; init; }

    public int Unchanged { get; init; }
}

public static class LineDiffBuilder
{
    public static LineDiffResult Build(string oldText, string newText)
    {
        var oldLines = SplitLines(oldText);
        var newLines = SplitLines(newText);

        var oldCount = oldLines.Length;
        var newCount = newLines.Length;

        var lcs = new int[oldCount + 1, newCount + 1];

        for (var i = oldCount - 1; i >= 0; i--)
        {
            for (var j = newCount - 1; j >= 0; j--)
            {
                if (string.Equals(oldLines[i], newLines[j], StringComparison.Ordinal))
                {
                    lcs[i, j] = lcs[i + 1, j + 1] + 1;
                }
                else
                {
                    lcs[i, j] = Math.Max(lcs[i + 1, j], lcs[i, j + 1]);
                }
            }
        }

        var builder = new System.Text.StringBuilder();
        var removed = 0;
        var added = 0;
        var unchanged = 0;

        var oldIndex = 0;
        var newIndex = 0;

        while (oldIndex < oldCount && newIndex < newCount)
        {
            if (string.Equals(oldLines[oldIndex], newLines[newIndex], StringComparison.Ordinal))
            {
                builder.Append("  ").AppendLine(oldLines[oldIndex]);
                unchanged++;
                oldIndex++;
                newIndex++;
            }
            else if (lcs[oldIndex + 1, newIndex] >= lcs[oldIndex, newIndex + 1])
            {
                builder.Append("- ").AppendLine(oldLines[oldIndex]);
                removed++;
                oldIndex++;
            }
            else
            {
                builder.Append("+ ").AppendLine(newLines[newIndex]);
                added++;
                newIndex++;
            }
        }

        while (oldIndex < oldCount)
        {
            builder.Append("- ").AppendLine(oldLines[oldIndex]);
            removed++;
            oldIndex++;
        }

        while (newIndex < newCount)
        {
            builder.Append("+ ").AppendLine(newLines[newIndex]);
            added++;
            newIndex++;
        }

        return new LineDiffResult
        {
            Text = builder.ToString(),
            Added = added,
            Removed = removed,
            Unchanged = unchanged
        };
    }

    private static string[] SplitLines(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        return text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
    }
}
