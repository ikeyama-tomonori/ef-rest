namespace EfRest.Example.Model;

using System.Text.Json.Serialization;

public class Genre
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? ParentGenreId { get; set; }
    public Genre? ParentGenre { get; set; }

    [JsonPropertyName("child_genres")]
    public ICollection<Genre>? ChildGenres { get; set; }

    public ICollection<Book>? Books { get; set; }
}
