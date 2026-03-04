using AutoMapper;
using ServerService.Models.Grid;
using ServerService.Repositories;

namespace ServerService.Services
{
    public class GenericService<TEntity, TDto, TCreateDto, TUpdateDto> 
        : IGenericService<TEntity, TDto, TCreateDto, TUpdateDto>
        where TEntity : class
        where TDto : class
        where TCreateDto : class
        where TUpdateDto : class
    {
        private readonly IRepository<TEntity> _repository;
        private readonly IMapper _mapper;

        public GenericService(IRepository<TEntity> repository, IMapper mapper)
        {
            _repository = repository;
            _mapper = mapper;
        }

        public async Task<TDto?> GetByIdAsync(int id)
        {
            var entity = await _repository.GetByIdAsync(id);
            return _mapper.Map<TDto?>(entity);
        }

        public async Task<(IEnumerable<TDto> Data, int Total, int Page, int PageSize)> GetPagedAsync(
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortOrder = null,
            JqGridFilter? filter = null)
        {
            if (!string.IsNullOrWhiteSpace(sortBy))
            {
                sortBy = char.ToUpper(sortBy[0]) + sortBy.Substring(1);
            }

            var entities = await _repository.GetPagedAsync(page, pageSize, sortBy, sortOrder, filter);
            var total = await _repository.CountAsync(filter);
            var dtos = _mapper.Map<IEnumerable<TDto>>(entities);
            return (dtos, total, page, pageSize);
        }

        public async Task<TDto> CreateAsync(TCreateDto createDto)
        {
            var entity = _mapper.Map<TEntity>(createDto);
            var created = await _repository.AddAsync(entity);
            return _mapper.Map<TDto>(created);
        }

        public async Task<TDto?> UpdateAsync(int id, TUpdateDto updateDto)
        {
            var existingEntity = await _repository.GetByIdAsync(id);
            if (existingEntity == null)
                return null;

            _mapper.Map(updateDto, existingEntity);
            await _repository.UpdateAsync(existingEntity);
            return _mapper.Map<TDto>(existingEntity);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            if (!await _repository.ExistsAsync(id))
                return false;

            await _repository.DeleteByIdAsync(id);
            return true;
        }
    }
}