namespace CommentIntelligence.Core.Evaluation;

public sealed record EvaluationExample(string GroupId, string Text, string ExpectedLabel);

public sealed record LabelMetrics(
    string Label,
    int Support,
    int Correct,
    int FalsePositives,
    int FalseNegatives)
{
    public double Precision => Correct + FalsePositives == 0
        ? 0
        : (double)Correct / (Correct + FalsePositives);

    public double Recall => Correct + FalseNegatives == 0
        ? 0
        : (double)Correct / (Correct + FalseNegatives);

    public double F1 => Precision + Recall == 0
        ? 0
        : 2 * Precision * Recall / (Precision + Recall);
}

public sealed record ClassificationEvaluationReport(
    int TotalExamples,
    int CorrectExamples,
    IReadOnlyList<string> Labels,
    IReadOnlyDictionary<string, LabelMetrics> Metrics,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> ConfusionMatrix)
{
    public double Accuracy => TotalExamples == 0 ? 0 : (double)CorrectExamples / TotalExamples;

    public double MacroF1 => Metrics.Count == 0 ? 0 : Metrics.Values.Average(metric => metric.F1);
}

public static class ClassificationEvaluator
{
    public static ClassificationEvaluationReport Evaluate(
        IEnumerable<EvaluationExample> examples,
        Func<string, string> predict)
    {
        ArgumentNullException.ThrowIfNull(examples);
        ArgumentNullException.ThrowIfNull(predict);

        var rows = examples.ToArray();
        var labels = rows.Select(row => row.ExpectedLabel)
            .Concat(rows.Select(row => predict(row.Text)))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(label => label, StringComparer.Ordinal)
            .ToArray();

        var matrix = labels.ToDictionary(
            expected => expected,
            _ => labels.ToDictionary(
                predicted => predicted,
                _ => 0,
                StringComparer.Ordinal),
            StringComparer.Ordinal);

        var correct = 0;
        foreach (var row in rows)
        {
            var predicted = predict(row.Text);
            matrix[row.ExpectedLabel][predicted]++;
            if (string.Equals(row.ExpectedLabel, predicted, StringComparison.Ordinal))
                correct++;
        }

        var metrics = labels.ToDictionary(
            label => label,
            label =>
            {
                var support = rows.Count(row => row.ExpectedLabel == label);
                var truePositive = matrix[label][label];
                var falsePositives = labels
                    .Where(expected => expected != label)
                    .Sum(expected => matrix[expected][label]);
                var falseNegatives = labels
                    .Where(predicted => predicted != label)
                    .Sum(predicted => matrix[label][predicted]);

                return new LabelMetrics(label, support, truePositive, falsePositives, falseNegatives);
            },
            StringComparer.Ordinal);

        var readOnlyMatrix = matrix.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyDictionary<string, int>)pair.Value,
            StringComparer.Ordinal);

        return new ClassificationEvaluationReport(rows.Length, correct, labels, metrics, readOnlyMatrix);
    }

    public static (IReadOnlyList<EvaluationExample> Train, IReadOnlyList<EvaluationExample> Validation)
        SplitByGroup(
            IEnumerable<EvaluationExample> examples,
            double validationFraction = 0.2)
    {
        if (validationFraction is <= 0 or >= 1)
            throw new ArgumentOutOfRangeException(nameof(validationFraction));

        var rows = examples.ToArray();
        var groups = rows.Select(row => row.GroupId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(group => StableHash(group))
            .ToArray();
        var validationGroupCount = Math.Clamp(
            (int)Math.Round(groups.Length * validationFraction, MidpointRounding.AwayFromZero),
            1,
            Math.Max(groups.Length - 1, 1));
        var validationGroups = groups.Take(validationGroupCount).ToHashSet(StringComparer.Ordinal);

        return (
            rows.Where(row => !validationGroups.Contains(row.GroupId)).ToArray(),
            rows.Where(row => validationGroups.Contains(row.GroupId)).ToArray());
    }

    private static uint StableHash(string value)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var character in value)
            {
                hash ^= character;
                hash *= 16777619;
            }

            return hash;
        }
    }
}
