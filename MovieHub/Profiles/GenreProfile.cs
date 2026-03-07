using AutoMapper;
using MovieHub.Data.Dtos.Genre;
using MovieHub.Models;

namespace MovieHub.Profiles;

public class GenreProfile : Profile
{
    public GenreProfile()
    {
        CreateMap<Genre, GenreResponseDto>();
        CreateMap<GenreRequestDto, Genre>();
    }
}