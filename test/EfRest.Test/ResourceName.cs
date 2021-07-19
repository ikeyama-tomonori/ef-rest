using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Collections.Generic;
using Microsoft.Extensions.Primitives;
using System.Threading.Tasks;
using System.Text.Json;

namespace EfRest.Test
{
    using Example.Model;

    [TestClass]
    public class ResourceName
    {
        [TestMethod]
        public async Task Resource_name_as_property_name()
        {
            using var db = new BookDbContext();
            using var handler = new EfRestHandler(db);
            using var client = new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost")
            };

            var response = await client.GetFromJsonAsync<Book[]>("/Books");
            Assert.IsNotNull(response);
        }

        [TestMethod]
        public async Task Resource_name_with_slash()
        {
            using var db = new BookDbContext();
            using var handler = new EfRestHandler(db);
            using var client = new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost")
            };

            var response = await client.GetFromJsonAsync<BookDetail[]>("/books/details");
            Assert.IsNotNull(response);
        }

        [TestMethod]
        public async Task Camelcase_name()
        {
            using var db = new BookDbContext();
            using var handler = new EfRestHandler(db)
            {
                JsonSerializerOptions = new(JsonSerializerDefaults.General)
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }
            };

            using var client = new HttpClient(handler)

            {
                BaseAddress = new Uri("http://localhost")
            };

            var response = await client.GetFromJsonAsync<Book[]>("/books");
            Assert.IsNotNull(response);
        }

        [TestMethod]
        public async Task Case_Insensitive_name()
        {
            using var db = new BookDbContext();
            using var handler = new EfRestHandler(db)
            {
                JsonSerializerOptions = new(JsonSerializerDefaults.General)
                {
                    PropertyNameCaseInsensitive = true
                }
            };

            using var client = new HttpClient(handler)

            {
                BaseAddress = new Uri("http://localhost")
            };

            var response = await client.GetFromJsonAsync<Book[]>("/books");
            Assert.IsNotNull(response);
        }

        [TestMethod]
        public async Task Not_start_with_slash()
        {
            using var db = new BookDbContext();
            using var handler = new EfRestHandler(db);
            using var client = new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost")
            };

            var response = await client.GetFromJsonAsync<Book[]>("Books/");
            Assert.IsNotNull(response);
        }
    }
}
