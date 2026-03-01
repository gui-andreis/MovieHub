using AutoMapper;
using MovieHub.Data.Dtos.Review;
using MovieHub.Models;

namespace MovieHub.Profiles;

public class ReviewProfile : Profile
{
    public ReviewProfile()
    {
        // Create
        CreateMap<CreateReviewDto, Review>();

        // Update
        CreateMap<UpdateReviewDto, Review>();

        // Response
        CreateMap<Review, ReviewResponseDto>()
            .ForMember(dest => dest.UserName,
                opt => opt.MapFrom(src => src.User != null ? src.User.UserName : string.Empty));
    }
}