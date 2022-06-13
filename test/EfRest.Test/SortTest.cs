using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using EfRest.Example.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EfRest.Test;

[TestClass]
public class SortTest
{
    public Book[] Books { get; }

    public SortTest()
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
    public async Task Order_Desc()
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

        var sort = HttpUtility.UrlEncode(JsonSerializer.Serialize(new[] { "Rating", "desc" }));
        var response = await client.GetFromJsonAsync<BookDetail[]>($"books/details?sort={sort}");
        response?.Aggregate((previous, current) =>
        {
            Assert.IsTrue(previous.Rating >= current.Rating);
            return current;
        });
    }

    [TestMethod]
    public async Task Order_Asc()
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

        var sort = HttpUtility.UrlEncode(JsonSerializer.Serialize(new[] { "TotalPages", "asc" }));
        var response = await client.GetFromJsonAsync<BookDetail[]>($"books/details?sort={sort}");
        response?.Aggregate((previous, current) =>
        {
            if (previous.TotalPages == null || current.TotalPages == null)
            {
                return current;
            }
            Assert.IsTrue(previous.TotalPages <= current.TotalPages);
            return current;
        });
    }

    [TestMethod]
    public async Task Json_invalid()
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

        var sort = HttpUtility.UrlEncode("{");
        var response = await client.GetAsync($"books/details/?sort={sort}");
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task Field_invalid()
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

        var sort = HttpUtility.UrlEncode(JsonSerializer.Serialize(new[] { "xxx", "asc" }));
        var response = await client.GetAsync($"books/details/?sort={sort}");
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task Order_invalid()
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

        var sort = HttpUtility.UrlEncode(JsonSerializer.Serialize(new[] { "TotalPages", "xxx" }));
        var response = await client.GetAsync($"books/details/?sort={sort}");
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task Array_invalid()
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

        var sort = HttpUtility.UrlEncode(JsonSerializer.Serialize(new[] { "TotalPages" }));
        var response = await client.GetAsync($"books/details/?sort={sort}");
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
