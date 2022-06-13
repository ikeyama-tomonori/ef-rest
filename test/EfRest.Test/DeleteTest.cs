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
public class DeleteTest
{
    [TestMethod]
    public async Task Delete()
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

        var response = await client.DeleteAsync($"Books/{book.Id}");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var deleted = await response.Content.ReadFromJsonAsync<Book>();
        Assert.AreEqual("New Book", deleted?.Title);
        Assert.IsNull(db.Books.FirstOrDefault(b => b.Title == "New Book"));
    }
}
