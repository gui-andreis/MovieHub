using AutoMapper;
using MovieHub.Data.Dtos.Review;
using MovieHub.Models;

namespace MovieHub.Profiles;

public class ReviewProfile : Profile
{
    public ReviewProfile()
    {
        // CreateReviewDto -> Review
        CreateMap<CreateReviewDto, Review>();

        // UpdateReviewDto -> Review
        CreateMap<UpdateReviewDto, Review>();

        // Review -> ReviewResponseDto
        CreateMap<Review, ReviewResponseDto>()
            .ForMember(dest => dest.UserName,
                opt => opt.MapFrom(src => src.User != null ? src.User.UserName : string.Empty));
    }
}