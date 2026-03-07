using AutoMapper;
using MovieHub.Models;
using MovieHub.Data.Dtos.Movie;

namespace MovieHub.Profiles;

public class MovieProfile : Profile
{
    public MovieProfile()
    {
        // DTO -> Entity (Create)
        CreateMap<CreateMovieDto, Movie>();

        // DTO -> Entity (Update)
        CreateMap<UpdateMovieDto, Movie>()
            .ForMember(dest => dest.Id, opt => opt.Ignore());

        // Entity -> DTO (Response)
        CreateMap<Movie, MovieResponseDto>()
            .ForMember(dest => dest.AverageRating,
                opt => opt.MapFrom(src => src.Reviews.Any()
                    ? src.Reviews.Average(r => r.Rating)
                    : 0))
            .ForMember(dest => dest.Genres,
                opt => opt.MapFrom(src => src.MovieGenres
                    .Select(mg => mg.Genre.Name)
                    .ToList()));
    }
}