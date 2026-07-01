using CommentIntelligence.Core.Models;

namespace CommentIntelligence.Core.Storage;

/// <summary>
/// Simple thread-safe in-memory store. Fine for the test harness and for prototyping;
/// swap in a persistent <see cref="IClassifiedCommentStore"/> for production.
/// </summary>
public sealed class InMemoryClassifiedCommentStore : IClassifiedCommentStore
{
    private readonly List<Comment> _comments = new();
    private readonly object _lock = new();

    public Task AddAsync(Comment comment, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _comments.Add(comment);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Comment>> GetAllAsync(CommentSortOption sortOption = CommentSortOption.MostUseful, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            IEnumerable<Comment> query = _comments;

            query = sortOption switch
            {
                CommentSortOption.MostUseful => query.OrderByDescending(c => c.Classification?.VisibilityScore ?? 0),
                CommentSortOption.Newest => query.OrderByDescending(c => c.CreatedAtUtc),
                CommentSortOption.Oldest => query.OrderBy(c => c.CreatedAtUtc),
                CommentSortOption.HighestStars => query.OrderByDescending(c => c.Classification?.PredictedStars ?? 0),
                CommentSortOption.LowestStars => query.OrderBy(c => c.Classification?.PredictedStars ?? 0),
                _ => query
            };

            return Task.FromResult<IReadOnlyList<Comment>>(query.ToList());
        }
    }
}
