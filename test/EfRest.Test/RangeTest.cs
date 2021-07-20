using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Net.Http;
using System.Collections.Generic;
using Microsoft.Extensions.Primitives;
using System.Threading.Tasks;
using System.Text.Json;
using System.Web;
using System.Linq.Expressions;
using System.Net.Http.Json;
using System.Net;

namespace EfRest.Test
{
    using Example.Model;

    [TestClass]
    public class RangeTest
    {
        public Book[] Books { get; }

        public RangeTest()
        {
            Books = new[]
            {
                new Book
                {
                    Title = "Anna Karenina",
                },
                new Book
                {
                    Title = "War and Peace",
                },
                new Book
                {
                    Title = "Pride and Prejudice",
                },
                new Book
                {
                    Title = "Sense and Sensibility",
                },
            };
        }

        [TestMethod]
        public async Task Get_several_records()
        {
            using var db = new BookDbContext();
            await db.Books.AddRangeAsync(Books);
            await db.SaveChangesAsync();

            using var handler = new EfRestHandler(db);
            using var client = new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost")
            };

            var range = HttpUtility.UrlEncode(JsonSerializer.Serialize(new[] { 1, 3 }));
            var response = await client.GetAsync($"/Books?range={range}");
            var data = await response.Content.ReadFromJsonAsync<Book[]>();
            Assert.AreEqual(3, data?.Length);
            var contentRange = response.Content.Headers.GetValues("Content-Range").First();
            Assert.AreEqual("items 1-3/4", contentRange);
        }

        [TestMethod]
        public async Task Get_first_record()
        {
            using var db = new BookDbContext();
            await db.Books.AddRangeAsync(Books);
            await db.SaveChangesAsync();

            using var handler = new EfRestHandler(db);
            using var client = new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost")
            };

            var range = HttpUtility.UrlEncode(JsonSerializer.Serialize(new[] { 0, 0 }));
            var response = await client.GetAsync($"/Books?range={range}");
            var data = await response.Content.ReadFromJsonAsync<Book[]>();
            Assert.AreEqual(1, data?.Length);
            var contentRange = response.Content.Headers.GetValues("Content-Range").First();
            Assert.AreEqual("items 0-0/4", contentRange);
        }

        [TestMethod]
        public async Task Get_last_record()
        {
            using var db = new BookDbContext();
            await db.Books.AddRangeAsync(Books);
            await db.SaveChangesAsync();

            using var handler = new EfRestHandler(db);
            using var client = new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost")
            };

            var range = HttpUtility.UrlEncode(JsonSerializer.Serialize(new[] { 3, 3 }));
            var response = await client.GetAsync($"/Books?range={range}");
            var data = await response.Content.ReadFromJsonAsync<Book[]>();
            Assert.AreEqual(1, data?.Length);
            var contentRange = response.Content.Headers.GetValues("Content-Range").First();
            Assert.AreEqual("items 3-3/4", contentRange);
        }

        [TestMethod]
        public async Task Over_range()
        {
            using var db = new BookDbContext();
            await db.Books.AddRangeAsync(Books);
            await db.SaveChangesAsync();

            using var handler = new EfRestHandler(db);
            using var client = new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost")
            };

            var range = HttpUtility.UrlEncode(JsonSerializer.Serialize(new[] { 0, 4 }));
            var response = await client.GetAsync($"/Books?range={range}");
            var data = await response.Content.ReadFromJsonAsync<Book[]>();
            Assert.AreEqual(4, data?.Length);
            var contentRange = response.Content.Headers.GetValues("Content-Range").First();
            Assert.AreEqual("items 0-3/4", contentRange);
        }

        [TestMethod]
        public async Task Out_of_range()
        {
            using var db = new BookDbContext();
            await db.Books.AddRangeAsync(Books);
            await db.SaveChangesAsync();

            using var handler = new EfRestHandler(db);
            using var client = new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost")
            };

            var range = HttpUtility.UrlEncode(JsonSerializer.Serialize(new[] { 4, 4 }));
            var response = await client.GetAsync($"/Books?range={range}");
            var data = await response.Content.ReadFromJsonAsync<Book[]>();
            Assert.AreEqual(0, data?.Length);
            var contentRange = response.Content.Headers.GetValues("Content-Range").First();
            Assert.AreEqual("items */4", contentRange);
        }

        [TestMethod]
        public async Task Json_invalid()
        {
            using var db = new BookDbContext();
            await db.Books.AddRangeAsync(Books);
            await db.SaveChangesAsync();

            using var handler = new EfRestHandler(db);
            using var client = new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost")
            };

            var range = HttpUtility.UrlEncode("{}");
            var response = await client.GetAsync($"/Books?range={range}");
            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [TestMethod]
        public async Task Array_invalid()
        {
            using var db = new BookDbContext();
            await db.Books.AddRangeAsync(Books);
            await db.SaveChangesAsync();

            using var handler = new EfRestHandler(db);
            using var client = new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost")
            };

            var range = HttpUtility.UrlEncode(JsonSerializer.Serialize(new[] { 4 }));
            var response = await client.GetAsync($"/Books?range={range}");
            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
