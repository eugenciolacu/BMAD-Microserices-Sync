using ServerService.Models.Grid;

namespace ServerService.Services
{
    public interface IGenericService<TEntity, TDto, TCreateDto, TUpdateDto>
        where TEntity : class
        where TDto : class
        where TCreateDto : class
        where TUpdateDto : class
    {
        Task<TDto?> GetByIdAsync(int id);
        Task<(IEnumerable<TDto> Data, int Total, int Page, int PageSize)> GetPagedAsync(
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortOrder = null,
            JqGridFilter? filter = null);
        Task<TDto> CreateAsync(TCreateDto createDto);
        Task<TDto?> UpdateAsync(int id, TUpdateDto updateDto);
        Task<bool> DeleteAsync(int id);
    }
}