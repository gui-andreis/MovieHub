using AutoMapper;
using MovieHub.Models;
using MovieHub.Data.Dtos.Movie;

namespace MovieHub.Profiles;

public class MovieProfile : Profile
{
    public MovieProfile()
    {
        // Create
        CreateMap<CreateMovieDto, Movie>();

        // Update
        CreateMap<UpdateMovieDto, Movie>();

        // Response
        // Response
        CreateMap<Movie, MovieResponseDto>()
            .ForMember(dest => dest.AverageRating,
                opt => opt.MapFrom(src => src.Reviews.Any()
                    ? src.Reviews.Average(r => r.Rating)
                    : 0));

        //patch movie pro update
    }
}