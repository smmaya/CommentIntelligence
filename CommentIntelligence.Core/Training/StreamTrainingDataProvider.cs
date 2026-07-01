using CommentIntelligence.Core.Models;

namespace CommentIntelligence.Core.Training;

/// <summary>
/// Loads training examples from a stream produced by a factory function — use this for
/// blob storage, embedded assembly resources, or any other non-filesystem source.
/// The factory is invoked fresh on every <see cref="LoadAsync"/> call and the resulting
/// stream is disposed automatically.
/// </summary>
public sealed class StreamTrainingDataProvider : ITrainingDataProvider
{
    private readonly Func<Stream> _streamFactory;

    public StreamTrainingDataProvider(Func<Stream> streamFactory)
    {
        _streamFactory = streamFactory;
    }

    public async Task<IReadOnlyList<TrainingExample>> LoadAsync(CancellationToken cancellationToken = default)
    {
        await using var stream = _streamFactory();
        return await CsvTrainingDataReader.ReadAsync(stream, cancellationToken);
    }
}
