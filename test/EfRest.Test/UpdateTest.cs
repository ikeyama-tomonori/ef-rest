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
using System.Text.Json.Serialization;

namespace EfRest.Test
{
    using Example.Model;

    [TestClass]
    public class UpdateTest
    {
        [TestMethod]
        public async Task Update()
        {
            using var db = new BookDbContext();
            using var handler = new EfRestHandler(db);
            using var client = new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost")
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

            var response = await client.PutAsJsonAsync($"/Books/{book.Id}", modifiedBook);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            var modified = await response.Content.ReadFromJsonAsync<Book>();
            Assert.AreEqual("Modified Book", modified?.Title);
        }

        [TestMethod]
        public async Task Update_with_navigation()
        {
            var jsonSerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                ReferenceHandler = ReferenceHandler.Preserve
            };
            using var db = new BookDbContext();
            using var handler = new EfRestHandler(db)
            {
                JsonSerializerOptions = jsonSerializerOptions
            };
            using var client = new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost")
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
            var embed = HttpUtility.UrlEncode(JsonSerializer.Serialize(new[] { "BookDetail" }));
            var response = await client.PutAsJsonAsync($"/Books/{book.Id}?embed={embed}", modifiedBook);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            var modified = await response.Content.ReadFromJsonAsync<Book>(jsonSerializerOptions);
            Assert.AreEqual("Modified Book", modified?.Title);
            Assert.AreEqual(1.0m, modified?.BookDetail?.Rating);
        }

        [TestMethod]
        public async Task Invalid_id_format()
        {
            using var db = new BookDbContext();
            using var handler = new EfRestHandler(db);
            using var client = new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost")
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

            var response = await client.PutAsJsonAsync($"/Books/xxx", modifiedBook);
            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        }

        [TestMethod]
        public async Task Invalid_id_number()
        {
            using var db = new BookDbContext();
            using var handler = new EfRestHandler(db);
            using var client = new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost")
            };

            var modifiedBook = new Book
            {
                Title = "Modified Book"
            };

            var response = await client.PutAsJsonAsync($"/Books/1", modifiedBook);
            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        }

        [TestMethod]
        public async Task Invalid_body_format()
        {
            using var db = new BookDbContext();
            using var handler = new EfRestHandler(db);
            using var client = new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost")
            };

            var book = new Book
            {
                Title = "New Book"
            };
            await db.Books.AddAsync(book);
            await db.SaveChangesAsync();

            var response = await client.PutAsJsonAsync($"/Books/{book.Id}", "xxx");
            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [TestMethod]
        public async Task Invalid_body_with_annotation()
        {
            using var db = new BookDbContext();
            using var handler = new EfRestHandler(db);
            using var client = new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost")
            };

            var book = new Book
            {
                Title = "New Book"
            };
            await db.Books.AddAsync(book);
            await db.SaveChangesAsync();

            var modifiedBook = new Book
            {
                Title = ""
            };
            var response = await client.PutAsJsonAsync($"/Books/{book.Id}", modifiedBook);
            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
