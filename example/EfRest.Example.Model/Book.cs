using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EfRest.Example.Model;

public class Book
{
    public int Id { get; set; }

    [Required]
    public string Title { get; set; } = "";

    public string? Description { get; set; }

    [NotMapped]
    public string? AuthorNames { get; set; }

    public ICollection<Author>? Authors { get; set; }
    public ICollection<Genre>? Genres { get; set; }
    public BookDetail? BookDetail { get; set; }
}
