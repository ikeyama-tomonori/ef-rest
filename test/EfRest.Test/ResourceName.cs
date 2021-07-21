using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EfRest.Test
{
    using Example.Model;

    [TestClass]
    public class ResourceName
    {
        [TestMethod]
        public async Task Resource_name_as_property_name()
        {
            var db = new BookDbContext();
            var baseAddress = new Uri("http://localhost/api/");
            var server = new EfRestServer(baseAddress)
            {
                CloudCqsOptions = Options.Instance
            };
            server.Init(db);
            var handler = server.GetHandler();
            using var client = new HttpClient(handler)
            {
                BaseAddress = baseAddress
            };

            var response = await client.GetFromJsonAsync<Book[]>("Books");
            Assert.IsNotNull(response);
        }

        [TestMethod]
        public async Task Resource_name_with_slash()
        {
            var db = new BookDbContext();
            var baseAddress = new Uri("http://localhost/api/");
            var server = new EfRestServer(baseAddress)
            {
                CloudCqsOptions = Options.Instance
            };
            server.Init(db);
            var handler = server.GetHandler();
            using var client = new HttpClient(handler)
            {
                BaseAddress = baseAddress
            };

            var response = await client.GetFromJsonAsync<BookDetail[]>("books/details");
            Assert.IsNotNull(response);
        }

        [TestMethod]
        public async Task Camelcase_name()
        {
            var db = new BookDbContext();
            var baseAddress = new Uri("http://localhost/api/");
            var server = new EfRestServer(baseAddress)
            {
                CloudCqsOptions = Options.Instance,
                JsonSerializerOptions = new(JsonSerializerDefaults.General)
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }
            };
            server.Init(db);
            var handler = server.GetHandler();
            using var client = new HttpClient(handler)
            {
                BaseAddress = baseAddress
            };

            var response = await client.GetFromJsonAsync<Book[]>("books");
            Assert.IsNotNull(response);
        }

        [TestMethod]
        public async Task Case_Insensitive_name()
        {
            var db = new BookDbContext();
            var baseAddress = new Uri("http://localhost/api/");
            var server = new EfRestServer(baseAddress)
            {
                CloudCqsOptions = Options.Instance,
                JsonSerializerOptions = new(JsonSerializerDefaults.General)
                {
                    PropertyNameCaseInsensitive = true
                }
            };
            server.Init(db);
            var handler = server.GetHandler();
            using var client = new HttpClient(handler)
            {
                BaseAddress = baseAddress
            };

            var response = await client.GetFromJsonAsync<Book[]>("books");
            Assert.IsNotNull(response);
        }

        [TestMethod]
        public async Task End_with_slash()
        {
            var db = new BookDbContext();
            var baseAddress = new Uri("http://localhost/api/");
            var server = new EfRestServer(baseAddress)
            {
                CloudCqsOptions = Options.Instance
            };
            server.Init(db);
            var handler = server.GetHandler();
            using var client = new HttpClient(handler)
            {
                BaseAddress = baseAddress
            };

            var response = await client.GetFromJsonAsync<Book[]>("Books/");
            Assert.IsNotNull(response);
        }
    }
}
