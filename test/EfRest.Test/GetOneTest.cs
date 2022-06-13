using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using EfRest.Example.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EfRest.Test;

[TestClass]
public class GetOneTest
{
    public Book[] Books { get; }

    public GetOneTest()
    {
        Books = new[]
{
                new Book
                {
                    Title = "Anna Karenina",
                    BookDetail = new()
                    {
                        TotalPages = 1000,
                        Rating = 2.0m,
                    }
                },
                new Book
                {
                    Title = "War and Peace",
                    BookDetail = new()
                    {
                        TotalPages = 2000,
                        Rating = 2.5m,
                        Publisher =new ()
                        {
                            Name = "Publisher1"
                        }
                    }
                },
                new Book
                {
                    Title = "Pride and Prejudice",
                    BookDetail = new()
                    {
                        Rating = 3.0m,
                    }
                },
                new Book
                {
                    Title = "Sense and Sensibility",
                    BookDetail = new()
                    {
                        TotalPages = 1000,
                        Rating = 3.5m,
                    }
                },
            };
    }

    [TestMethod]
    public async Task Normal_id()
    {
        var db = new BookDbContext();
        var baseAddress = new Uri("http://localhost/api/");
        var server = new EfRestServer(db)
        {
            CloudCqsOptions = Options.Instance,
        };
        var handler = new EfRestHandler(server, baseAddress);
        using var client = new HttpClient(handler)
        {
            BaseAddress = baseAddress
        };

        await db.Books.AddRangeAsync(Books);
        await db.SaveChangesAsync();

        var id = Books.Single(b => b.Title == "War and Peace").Id;
        var response = await client.GetFromJsonAsync<Book>($"Books/{id}");
        Assert.AreEqual("War and Peace", response?.Title);
    }

    [TestMethod]
    public async Task Invalid_id_format()
    {
        var db = new BookDbContext();
        var baseAddress = new Uri("http://localhost/api/");
        var server = new EfRestServer(db)
        {
            CloudCqsOptions = Options.Instance,
        };
        var handler = new EfRestHandler(server, baseAddress);
        using var client = new HttpClient(handler)
        {
            BaseAddress = baseAddress
        };

        await db.Books.AddRangeAsync(Books);
        await db.SaveChangesAsync();

        var response = await client.GetAsync($"Books/x");
        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task Invalid_id_out_of_range()
    {
        var db = new BookDbContext();
        var baseAddress = new Uri("http://localhost/api/");
        var server = new EfRestServer(db)
        {
            CloudCqsOptions = Options.Instance,
        };
        var handler = new EfRestHandler(server, baseAddress);

        using var client = new HttpClient(handler)
        {
            BaseAddress = baseAddress
        };

        await db.Books.AddRangeAsync(Books);
        await db.SaveChangesAsync();

        var maxId = db.Books.Max(b => b.Id);

        var response = await client.GetAsync($"Books/{maxId + 1}");
        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }
}
