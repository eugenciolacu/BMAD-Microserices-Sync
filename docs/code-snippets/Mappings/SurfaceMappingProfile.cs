using AutoMapper;
using ServerService.DTOs;
using ServerService.Models;

namespace ServerService.Mappings
{
    public class SurfaceMappingProfile : Profile
    {
        public SurfaceMappingProfile()
        {
            CreateMap<SurfaceCreateDto, Surface>();
            CreateMap<SurfaceUpdateDto, Surface>();
            CreateMap<Surface, SurfaceDto>();
        }
    }
}