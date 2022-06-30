namespace EfRest.Example.Model;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class Book
{
    public int Id { get; set; }

    [Required]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string TitleAndAuthor => $"{this.Title} by {this.AuthorNames}";

    [NotMapped]
    public string? AuthorNames { get; set; }

    public ICollection<Author>? Authors { get; set; }
    public ICollection<Genre>? Genres { get; set; }
    public BookDetail? BookDetail { get; set; }
}
