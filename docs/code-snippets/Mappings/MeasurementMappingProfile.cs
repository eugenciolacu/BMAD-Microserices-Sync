using AutoMapper;
using ServerService.DTOs;
using ServerService.Models;

namespace ServerService.Mappings
{
    public class MeasurementMappingProfile : Profile
    {
        public MeasurementMappingProfile()
        {
            CreateMap<MeasurementCreateDto, Measurement>();
            CreateMap<MeasurementUpdateDto, Measurement>();
            CreateMap<Measurement, MeasurementDto>();
        }
    }
}
