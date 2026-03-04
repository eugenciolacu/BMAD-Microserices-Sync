using AutoMapper;
using ServerService.DTOs;
using ServerService.Models;

namespace ServerService.Mappings
{
    public class UserMappingProfile : Profile
    {
        public UserMappingProfile()
        {
            CreateMap<UserCreateDto, User>();
            CreateMap<UserUpdateDto, User>();
            CreateMap<User, UserDto>();
        }
    }
}
