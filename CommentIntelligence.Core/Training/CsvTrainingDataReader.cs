using System.Text;

namespace CommentIntelligence.Core.Training;

/// <summary>
/// Loads training examples from CSV.
/// Expected columns: text,label.
/// The header row is optional.
/// Supports quoted fields with embedded commas and escaped quotes.
/// </summary>
internal static class CsvTrainingDataReader
{
    public static async Task<IReadOnlyList<TrainingExample>> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var results = new List<TrainingExample>();

        using var reader = new StreamReader(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            detectEncodingFromByteOrderMarks: true);

        var isFirstLine = true;
        var lineNumber = 0;
        string? line;

        while ((line = await ReadRecordAsync(reader, cancellationToken)) != null)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var fields = ParseLine(line);

            if (isFirstLine)
            {
                isFirstLine = false;

                if (fields.Count >= 2 &&
                    fields[0].Trim().Equals("text", StringComparison.OrdinalIgnoreCase) &&
                    fields[^1].Trim().Equals("label", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            if (fields.Count < 2)
            {
                throw new FormatException($"Training CSV line {lineNumber} must contain text and label columns.");
            }

            // The label is always the final column. Joining preceding fields also
            // accepts legacy rows whose text contains commas but was not quoted.
            var text = string.Join(',', fields.Take(fields.Count - 1)).Trim();
            var label = fields[^1].Trim();

            if (text.Length == 0 || label.Length == 0)
            {
                throw new FormatException($"Training CSV line {lineNumber} contains an empty text or label.");
            }

            results.Add(new TrainingExample
            {
                Text = text,
                Label = label
            });
        }

        return results;
    }

    private static async Task<string?> ReadRecordAsync(
        StreamReader reader,
        CancellationToken cancellationToken)
    {
        var firstLine = await reader.ReadLineAsync(cancellationToken);
        if (firstLine is null)
            return null;

        var record = new StringBuilder(firstLine);
        while (!HasBalancedQuotes(record))
        {
            var continuation = await reader.ReadLineAsync(cancellationToken);
            if (continuation is null)
                throw new FormatException("Training CSV contains an unterminated quoted field.");

            record.Append('\n').Append(continuation);
        }

        return record.ToString();
    }

    private static bool HasBalancedQuotes(StringBuilder record)
    {
        var quoted = false;
        for (var i = 0; i < record.Length; i++)
        {
            if (record[i] != '"')
                continue;

            if (quoted && i + 1 < record.Length && record[i + 1] == '"')
            {
                i++;
                continue;
            }

            quoted = !quoted;
        }

        return !quoted;
    }

    private static List<string> ParseLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return fields;
    }
}