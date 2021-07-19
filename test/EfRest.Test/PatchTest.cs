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
    public class PatchTest
    {
        [TestMethod]
        public async Task Patch()
        {
            using var db = new BookDbContext();
            using var handler = new EfRestHandler(db);
            using var client = new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost")
            };

            var book = new Book
            {
                Title = "New Book",
                Description = "This is new book."
            };
            await db.Books.AddAsync(book);
            await db.SaveChangesAsync();

            var modifiedBook = new
            {
                Description = "This is modified book."
            };
            var content = JsonContent.Create(modifiedBook);
            var response = await client.PatchAsync($"/Books/{book.Id}", content);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            var modified = await response.Content.ReadFromJsonAsync<Book>();
            Assert.AreEqual("New Book", modified?.Title);
            Assert.AreEqual("This is modified book.", modified?.Description);
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

            var modifiedBook = new
            {
                Title = "Modified Book",
                BookDetail = new BookDetail
                {
                    Rating = 1.0m
                }
            };
            var embed = HttpUtility.UrlEncode(JsonSerializer.Serialize(new[] { "BookDetail" }));
            var content = JsonContent.Create(modifiedBook);
            var response = await client.PatchAsync($"/Books/{book.Id}?embed={embed}", content);
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

            var modifiedBook = new
            {
                Description = "This is modified book."
            };

            var content = JsonContent.Create(modifiedBook);
            var response = await client.PatchAsync($"/Books/xxx", content);
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

            var modifiedBook = new
            {
                Description = "This is modified book."
            };

            var content = JsonContent.Create(modifiedBook);
            var response = await client.PatchAsync($"/Books/1", content);
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

            var content = JsonContent.Create("xxx");
            var response = await client.PatchAsync($"/Books/1", content);
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

            var modifiedBook = new
            {
                Title = ""
            };
            var content = JsonContent.Create(modifiedBook);
            var response = await client.PatchAsync($"/Books/{book.Id}", content);
            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [TestMethod]
        public async Task Invalid_field_format()
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

            var modifiedBook = new
            {
                Title = 1
            };
            var content = JsonContent.Create(modifiedBook);
            var response = await client.PatchAsync($"/Books/{book.Id}", content);
            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [TestMethod]
        public async Task Invalid_field_name()
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

            var modifiedBook = new
            {
                x = 1
            };
            var content = JsonContent.Create(modifiedBook);
            var response = await client.PatchAsync($"/Books/{book.Id}", content);
            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
