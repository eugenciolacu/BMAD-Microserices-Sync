using ServerService.Models.Grid;

namespace ServerService.Repositories
{
    public interface IRepository<T> where T : class
    {
        Task<T?> GetByIdAsync(int id);
        Task<IEnumerable<T>> GetPagedAsync(int pageNumber, int pageSize, string? sortBy = null, string? sortOrder = null, JqGridFilter? filter = null);
        Task<int> CountAsync();
        Task<int> CountAsync(JqGridFilter? filter);
        Task<T> AddAsync(T entity);
        Task UpdateAsync(T entity);
        Task DeleteAsync(T entity);
        Task DeleteByIdAsync(int id);
        Task<bool> ExistsAsync(int id);
    }
}