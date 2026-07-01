using CommentIntelligence.Core.Models;

namespace CommentIntelligence.Core.Training;

/// <summary>Loads training examples from a CSV file at a given path.</summary>
public sealed class FileTrainingDataProvider : ITrainingDataProvider
{
    private readonly string _filePath;

    public FileTrainingDataProvider(string filePath)
    {
        _filePath = filePath;
    }

    public async Task<IReadOnlyList<TrainingExample>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            throw new FileNotFoundException($"Training data file not found: {_filePath}", _filePath);
        }

        await using var stream = File.OpenRead(_filePath);
        return await CsvTrainingDataReader.ReadAsync(stream, cancellationToken);
    }
}
