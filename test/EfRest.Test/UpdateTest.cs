using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using EfRest.Example.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EfRest.Test;

[TestClass]
public class UpdateTest
{
    [TestMethod]
    public async Task Update()
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

        var book = new Book
        {
            Title = "New Book"
        };
        await db.Books.AddAsync(book);
        await db.SaveChangesAsync();

        var modifiedBook = new Book
        {
            Title = "Modified Book"
        };

        var response = await client.PutAsJsonAsync($"Books/{book.Id}", modifiedBook);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var modified = await response.Content.ReadFromJsonAsync<Book>();
        Assert.AreEqual("Modified Book", modified?.Title);
    }

    [TestMethod]
    public async Task Update_with_navigation()
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

        var book = new Book
        {
            Title = "New Book"
        };
        await db.Books.AddAsync(book);
        await db.SaveChangesAsync();

        var modifiedBook = new Book
        {
            Title = "Modified Book",
            BookDetail = new()
            {
                Rating = 1.0m
            }
        };

        var response = await client.PutAsJsonAsync($"Books/{book.Id}", modifiedBook);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var modified = await response.Content.ReadFromJsonAsync<Book>();
        Assert.AreEqual("Modified Book", modified?.Title);
        Assert.AreEqual(1.0m, book.BookDetail?.Rating);
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

        var book = new Book
        {
            Title = "New Book"
        };
        await db.Books.AddAsync(book);
        await db.SaveChangesAsync();

        var modifiedBook = new Book
        {
            Title = "Modified Book"
        };

        var response = await client.PutAsJsonAsync($"Books/xxx", modifiedBook);
        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task Invalid_id_number()
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

        var modifiedBook = new Book
        {
            Title = "Modified Book"
        };

        var response = await client.PutAsJsonAsync($"Books/1", modifiedBook);
        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task Invalid_body_format()
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

        var book = new Book
        {
            Title = "New Book"
        };
        await db.Books.AddAsync(book);
        await db.SaveChangesAsync();

        var response = await client.PutAsJsonAsync($"Books/{book.Id}", "xxx");
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
