using AutoMapper;
using ServerService.DTOs;
using ServerService.Models;

namespace ServerService.Mappings
{
    public class BuildingMappingProfile : Profile
    {
        public BuildingMappingProfile()
        {
            CreateMap<BuildingCreateDto, Building>();
            CreateMap<BuildingUpdateDto, Building>();
            CreateMap<Building, BuildingDto>();
        }
    }
}