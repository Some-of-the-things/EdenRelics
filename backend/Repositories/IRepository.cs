using System.Linq.Expressions;
using Eden_Relics_BE.Data.Entities;

namespace Eden_Relics_BE.Repositories;

public interface IRepository<T> where T : BaseEntity
{
    /// <summary>
    /// Composable query root with the soft-delete filter applied. Lets services build
    /// Include / Where / Select / aggregate queries that execute in the database instead
    /// of materialising whole tables. Prefer this for anything beyond a simple by-id or
    /// single-predicate lookup. Because every repository shares the request's DbContext,
    /// queries from different repositories compose (e.g. cross-entity subqueries).
    /// </summary>
    IQueryable<T> Query();

    /// <param name="includeDeleted">When true, bypasses the IsDeleted query filter.</param>
    IQueryable<T> Query(bool includeDeleted);

    Task<T?> GetByIdAsync(Guid id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<IEnumerable<T>> GetAllAsync(bool includeDeleted);
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, bool includeDeleted);
    Task<int> CountAsync(Expression<Func<T, bool>> predicate);
    Task<T> AddAsync(T entity);

    /// <summary>
    /// Inserts the given entities and persists in a single SaveChanges. That same save
    /// also flushes any pending edits already tracked on this context, so a bulk upsert
    /// (mutate loaded rows, add the new ones, call this once) commits in one transaction.
    /// </summary>
    Task AddRangeAsync(IEnumerable<T> entities);
    Task UpdateAsync(T entity);
    Task DeleteAsync(Guid id);
}
