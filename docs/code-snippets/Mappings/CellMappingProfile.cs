using AutoMapper;
using ServerService.DTOs;
using ServerService.Models;

namespace ServerService.Mappings
{
    public class CellMappingProfile : Profile
    {
        public CellMappingProfile()
        {
            CreateMap<CellCreateDto, Cell>();
            CreateMap<CellUpdateDto, Cell>();
            CreateMap<Cell, CellDto>();
        }
    }
}
