namespace CommentIntelligence.Core.Classification;

/// <summary>
/// Plain-data multinomial Naive Bayes model: raw word/document counts per class.
/// Deliberately stored as counts (not pre-computed log-probabilities) so the model
/// is human-inspectable and trivially JSON-serializable for persistence/caching.
/// </summary>
public sealed class NaiveBayesModel
{
    public Dictionary<string, int> ClassDocumentCounts { get; init; } = new();
 
    public Dictionary<string, Dictionary<string, int>> ClassWordCounts { get; init; } = new();
 
    public Dictionary<string, long> ClassTotalWordCounts { get; init; } = new();
 
    public HashSet<string> Vocabulary { get; init; } = new();
 
    public int TotalDocuments => ClassDocumentCounts.Values.Sum();
}
