using System.ComponentModel.DataAnnotations;
using MovieHub.Models;

namespace MovieHub.Dtos;

// ---------- Auth ----------

public class RegisterRequest
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 6)]
    public string Password { get; set; } = string.Empty;
}

public class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class RefreshRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}

public record UserDto(string Id, string Name, string Email, IReadOnlyList<string> Roles);

public record AuthResult(string AccessToken, string RefreshToken, DateTime ExpiresAt, UserDto User);

// ---------- Profiles ----------

public record ProfileDto(Guid Id, string Name, string AvatarKey, bool IsKids, string MaturityLimit, string Language, DateTime CreatedAt);

public class ProfileRequest
{
    [Required, StringLength(50, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(40)]
    public string? AvatarKey { get; set; }

    public bool IsKids { get; set; }

    /// <summary>L, 10, 12, 14, 16 ou 18.</summary>
    public string? MaturityLimit { get; set; }

    [StringLength(10)]
    public string? Language { get; set; }
}

// ---------- Me ----------

public record MyListStatusDto(int TitleId, bool InMyList);

public class ProgressRequest
{
    [Range(1, int.MaxValue)]
    public int TitleId { get; set; }

    public int? EpisodeId { get; set; }

    [Range(0, int.MaxValue)]
    public int PositionSeconds { get; set; }

    [Range(1, int.MaxValue)]
    public int DurationSeconds { get; set; }
}

public record EpisodeRefDto(int Id, int SeasonNumber, int EpisodeNumber, string Name);

public record ProgressResultDto(int TitleId, int? EpisodeId, int PositionSeconds, int DurationSeconds, int Percent, bool Completed, EpisodeRefDto? NextEpisode);

public record ContinueWatchingDto(TitleCardDto Title, EpisodeRefDto? Episode, int PositionSeconds, int DurationSeconds, int Percent, DateTime UpdatedAt);

public class ReactionRequest
{
    [Required]
    public ReactionValue? Value { get; set; }
}

public record ReactionDto(int TitleId, ReactionValue? Value);

public record GenreCountDto(string Name, int Count);

public record MeStatsDto(int WatchedCount, int MinutesWatched, List<GenreCountDto> TopGenres, int ReviewsCount, int ListCount, int LikesCount);

// ---------- Reviews ----------

public class ReviewRequest
{
    [Range(1, 10)]
    public int Rating { get; set; }

    [StringLength(4000)]
    public string? Comment { get; set; }

    public bool ContainsSpoilers { get; set; }
}

public record ReviewAuthorDto(string ProfileName, string AvatarKey);

public record ReviewDto(int Id, int TitleId, int Rating, string? Comment, bool ContainsSpoilers, DateTime CreatedAt, DateTime? UpdatedAt, ReviewAuthorDto Author, bool IsMine);

public record MyReviewDto(int Id, int Rating, string? Comment, bool ContainsSpoilers, DateTime CreatedAt, DateTime? UpdatedAt, TitleCardDto Title);

public record ReviewSummaryDto(double Average, int Count, Dictionary<string, int> Distribution);
