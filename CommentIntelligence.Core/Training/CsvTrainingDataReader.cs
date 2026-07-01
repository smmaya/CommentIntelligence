using System.Text;
using CommentIntelligence.Core.Models;

namespace CommentIntelligence.Core.Training;

/// <summary>
/// Minimal CSV parser shared by the file/stream training data providers.
/// Expects two columns: text, label (an optional header row starting with
/// "text," is skipped automatically). Supports quoted fields with embedded
/// commas/quotes since review text often contains both.
/// </summary>
internal static class CsvTrainingDataReader
{
    public static async Task<IReadOnlyList<TrainingExample>> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        var results = new List<TrainingExample>();
        using var reader = new StreamReader(stream);

        var isFirstLine = true;
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (isFirstLine)
            {
                isFirstLine = false;
                if (line.StartsWith("text,", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            var fields = ParseLine(line);
            if (fields.Count < 2)
            {
                continue;
            }

            var text = fields[0].Trim();
            var label = fields[^1].Trim(); // last column is always the label
            if (text.Length == 0 || label.Length == 0)
            {
                continue;
            }

            results.Add(new TrainingExample { Text = text, Label = label });
        }

        return results;
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
