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
        CreateMap<Movie, MovieResponseDto>();

        //patch movie pro update
    }
}