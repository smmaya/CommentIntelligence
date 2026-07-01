using CommentIntelligence.Core.Models;

namespace CommentIntelligence.Core.Storage;

/// <summary>
/// Storage seam for classified comments. The package ships only an in-memory
/// implementation for the demo/test harness — host apps should provide their own
/// (EF Core, Dapper, whatever) backed implementation for production use.
/// </summary>
public interface IClassifiedCommentStore
{
    Task AddAsync(Comment comment, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Comment>> GetAllAsync(CommentSortOption sortOption = CommentSortOption.MostUseful, CancellationToken cancellationToken = default);
}
