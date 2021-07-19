using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EfRest.Example.Model;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EfRest.Test
{
    public class BookDbContext : DbContext
    {
        public DbSet<Publisher> Publishers { get; set; } = null!;

        public DbSet<Book> Books { get; set; } = null!;

        [JsonPropertyName("books/details")]
        public DbSet<BookDetail> BookDetails { get; set; } = null!;

        public DbSet<Author> Authors { get; set; } = null!;
        public DbSet<Genre> Genres { get; set; } = null!;

        public string DbName { get; }

        public BookDbContext()
        {
            DbName = Guid.NewGuid().ToString();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder
                .UseInMemoryDatabase(DbName)
                .ConfigureWarnings(builder =>
                {
                    builder.Log(InMemoryEventId.TransactionIgnoredWarning);
                })
                .LogTo(Console.WriteLine)
                .EnableSensitiveDataLogging();
        }
    }
}
