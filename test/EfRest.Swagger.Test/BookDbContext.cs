namespace EfRest.Swagger.Test;

using System.Text.Json.Serialization;
using EfRest.Example.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

public class BookDbContext : DbContext
{
    public BookDbContext()
    {
        this.DbName = Guid.NewGuid().ToString();
    }

    public DbSet<Publisher> Publishers => this.Set<Publisher>();

    public DbSet<Book> Books => this.Set<Book>();

    [JsonPropertyName("books/details")]
    public DbSet<BookDetail> BookDetails => this.Set<BookDetail>();

    public DbSet<Author> Authors => this.Set<Author>();
    public DbSet<Genre> Genres => this.Set<Genre>();

    public string DbName { get; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder
            .UseInMemoryDatabase(this.DbName)
            .ConfigureWarnings(builder =>
            {
                builder.Log(InMemoryEventId.TransactionIgnoredWarning);
            })
            .LogTo(Console.WriteLine)
            .EnableSensitiveDataLogging();
    }
}
