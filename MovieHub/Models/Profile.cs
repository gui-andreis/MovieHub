namespace MovieHub.Models;

/// <summary>Perfil de visualização ("Quem está assistindo?"). Tudo que é pessoal pertence a um perfil.</summary>
public class Profile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string AvatarKey { get; set; } = "avatar-01";
    public bool IsKids { get; set; }
    public string MaturityLimit { get; set; } = "18";
    public string Language { get; set; } = "pt-BR";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<MyListItem> MyList { get; set; } = new List<MyListItem>();
    public ICollection<WatchProgress> Progress { get; set; } = new List<WatchProgress>();
    public ICollection<Reaction> Reactions { get; set; } = new List<Reaction>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
}

public class MyListItem
{
    public Guid ProfileId { get; set; }
    public Profile Profile { get; set; } = null!;
    public int TitleId { get; set; }
    public Title Title { get; set; } = null!;
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}

public class WatchProgress
{
    public int Id { get; set; }
    public Guid ProfileId { get; set; }
    public Profile Profile { get; set; } = null!;
    public int TitleId { get; set; }
    public Title Title { get; set; } = null!;
    public int? EpisodeId { get; set; }
    public Episode? Episode { get; set; }
    public int PositionSeconds { get; set; }
    public int DurationSeconds { get; set; }
    public bool Completed { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class Reaction
{
    public Guid ProfileId { get; set; }
    public Profile Profile { get; set; } = null!;
    public int TitleId { get; set; }
    public Title Title { get; set; } = null!;
    public ReactionValue Value { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class Review
{
    public int Id { get; set; }
    public Guid ProfileId { get; set; }
    public Profile Profile { get; set; } = null!;
    public int TitleId { get; set; }
    public Title Title { get; set; } = null!;
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public bool ContainsSpoilers { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
