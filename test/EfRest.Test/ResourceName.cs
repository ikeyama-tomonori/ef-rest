namespace EfRest.Test;

using System.Net.Http.Json;
using System.Text.Json;
using EfRest.Example.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class ResourceName
{
    [TestMethod]
    public async Task Resource_name_as_property_name()
    {
        var db = new BookDbContext();
        var baseAddress = new Uri("http://localhost/api/");
        var server = new EfRestServer(db) { CloudCqsOptions = Options.Instance, };
        var handler = new EfRestHandler(server, baseAddress);
        using var client = new HttpClient(handler) { BaseAddress = baseAddress };

        var response = await client.GetFromJsonAsync<Book[]>("Books");
        Assert.IsNotNull(response);
    }

    [TestMethod]
    public async Task Resource_name_with_slash()
    {
        var db = new BookDbContext();
        var baseAddress = new Uri("http://localhost/api/");
        var server = new EfRestServer(db) { CloudCqsOptions = Options.Instance, };
        var handler = new EfRestHandler(server, baseAddress);
        using var client = new HttpClient(handler) { BaseAddress = baseAddress };

        var response = await client.GetFromJsonAsync<BookDetail[]>("books/details");
        Assert.IsNotNull(response);
    }

    [TestMethod]
    public async Task Camelcase_name()
    {
        var db = new BookDbContext();
        var baseAddress = new Uri("http://localhost/api/");
        var server = new EfRestServer(db)
        {
            CloudCqsOptions = Options.Instance,
            JsonSerializerOptions = new(JsonSerializerDefaults.General)
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            },
        };
        var handler = new EfRestHandler(server, baseAddress);
        using var client = new HttpClient(handler) { BaseAddress = baseAddress };

        var response = await client.GetFromJsonAsync<Book[]>("books");
        Assert.IsNotNull(response);
    }

    [TestMethod]
    public async Task Case_Insensitive_name()
    {
        var db = new BookDbContext();
        var baseAddress = new Uri("http://localhost/api/");
        var server = new EfRestServer(db)
        {
            CloudCqsOptions = Options.Instance,
            JsonSerializerOptions = new(JsonSerializerDefaults.General)
            {
                PropertyNameCaseInsensitive = true,
            },
        };
        var handler = new EfRestHandler(server, baseAddress);
        using var client = new HttpClient(handler) { BaseAddress = baseAddress };

        var response = await client.GetFromJsonAsync<Book[]>("books");
        Assert.IsNotNull(response);
    }

    [TestMethod]
    public async Task End_with_slash()
    {
        var db = new BookDbContext();
        var baseAddress = new Uri("http://localhost/api/");
        var server = new EfRestServer(db) { CloudCqsOptions = Options.Instance, };
        var handler = new EfRestHandler(server, baseAddress);
        using var client = new HttpClient(handler) { BaseAddress = baseAddress };

        var response = await client.GetFromJsonAsync<Book[]>("Books/");
        Assert.IsNotNull(response);
    }
}
