using AutoMapper;
using ServerService.DTOs;
using ServerService.Models;

namespace ServerService.Mappings
{
    public class RoomMappingProfile : Profile
    {
        public RoomMappingProfile()
        {
            CreateMap<RoomCreateDto, Room>();
            CreateMap<RoomUpdateDto, Room>();
            CreateMap<Room, RoomDto>();
        }
    }
}