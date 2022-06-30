namespace EfRest.Example.Model;

public class Author
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }
    public ICollection<Book>? Books { get; set; }
}
