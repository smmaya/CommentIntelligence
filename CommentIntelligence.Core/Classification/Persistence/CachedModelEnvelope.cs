using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CommentIntelligence.Core.Models;

namespace CommentIntelligence.Core.Classification.Persistence;

/// <summary>
/// On-disk wrapper around a trained <see cref="NaiveBayesModel"/>: the model itself plus
/// a SHA256 fingerprint of the training data that produced it. On load, the fingerprint
/// of the *current* training data is recomputed and compared — if it matches, the cached
/// model is reused as-is (skips retraining); if it doesn't, the cache is stale and the
/// caller should retrain.
/// </summary>
public sealed class CachedModelEnvelope
{
    public required string TrainingDataFingerprint { get; init; }

    public required NaiveBayesModel Model { get; init; }
}

public sealed class NaiveBayesModelCache
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string ComputeFingerprint(IReadOnlyList<TrainingExample> examples)
    {
        // Order-independent: sort first so re-saving the same CSV with reordered rows
        // doesn't falsely invalidate the cache.
        var builder = new StringBuilder();
        foreach (var example in examples.OrderBy(e => e.Label, StringComparer.Ordinal)
                                         .ThenBy(e => e.Text, StringComparer.Ordinal))
        {
            builder.Append(example.Label).Append('\u0001').Append(example.Text).Append('\u0002');
        }

        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    public async Task<CachedModelEnvelope?> TryLoadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<CachedModelEnvelope>(stream, JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            // Corrupt or incompatible cache file — treat as a cache miss rather than crashing.
            return null;
        }
    }

    public async Task SaveAsync(string path, CachedModelEnvelope envelope, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Write to a temp file then move, so a crash mid-write never leaves a corrupt
        // cache file behind for the next startup to choke on.
        var tempPath = path + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, envelope, JsonOptions, cancellationToken);
        }

        File.Move(tempPath, path, overwrite: true);
    }
}