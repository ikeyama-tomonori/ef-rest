using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EfRest.Test
{
    using Example.Model;

    [TestClass]
    public class PatchTest
    {
        [TestMethod]
        public async Task Patch()
        {
            var db = new BookDbContext();
            var baseAddress = new Uri("http://localhost/api/");
            var server = new EfRestServer(baseAddress)
            {
                CloudCqsOptions = Options.Instance,
            };
            server.Init(db);
            var handler = server.GetHandler();
            using var client = new HttpClient(handler)
            {
                BaseAddress = baseAddress
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
            var response = await client.PatchAsync($"Books/{book.Id}", content);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            var modified = await response.Content.ReadFromJsonAsync<Book>();
            Assert.AreEqual("New Book", modified?.Title);
            Assert.AreEqual("This is modified book.", modified?.Description);
        }

        [TestMethod]
        public async Task Update_with_navigation()
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
            var content = JsonContent.Create(modifiedBook);
            var response = await client.PatchAsync($"Books/{book.Id}", content);
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
            var server = new EfRestServer(baseAddress)
            {
                CloudCqsOptions = Options.Instance,
            };
            server.Init(db);
            var handler = server.GetHandler();
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

            var modifiedBook = new
            {
                Description = "This is modified book."
            };

            var content = JsonContent.Create(modifiedBook);
            var response = await client.PatchAsync($"Books/xxx", content);
            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        }

        [TestMethod]
        public async Task Invalid_id_number()
        {
            var db = new BookDbContext();
            var baseAddress = new Uri("http://localhost/api/");
            var server = new EfRestServer(baseAddress)
            {
                CloudCqsOptions = Options.Instance,
            };
            server.Init(db);
            var handler = server.GetHandler();
            using var client = new HttpClient(handler)
            {
                BaseAddress = baseAddress
            };

            var modifiedBook = new
            {
                Description = "This is modified book."
            };

            var content = JsonContent.Create(modifiedBook);
            var response = await client.PatchAsync($"Books/1", content);
            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        }

        [TestMethod]
        public async Task Invalid_body_format()
        {
            var db = new BookDbContext();
            var baseAddress = new Uri("http://localhost/api/");
            var server = new EfRestServer(baseAddress)
            {
                CloudCqsOptions = Options.Instance,
            };
            server.Init(db);
            var handler = server.GetHandler();
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

            var content = JsonContent.Create("xxx");
            var response = await client.PatchAsync($"Books/1", content);
            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [TestMethod]
        public async Task Invalid_body_with_annotation()
        {
            var db = new BookDbContext();
            var baseAddress = new Uri("http://localhost/api/");
            var server = new EfRestServer(baseAddress)
            {
                CloudCqsOptions = Options.Instance,
            };
            server.Init(db);
            var handler = server.GetHandler();
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

            var modifiedBook = new
            {
                Title = ""
            };
            var content = JsonContent.Create(modifiedBook);
            var response = await client.PatchAsync($"Books/{book.Id}", content);
            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [TestMethod]
        public async Task Invalid_field_format()
        {
            var db = new BookDbContext();
            var baseAddress = new Uri("http://localhost/api/");
            var server = new EfRestServer(baseAddress)
            {
                CloudCqsOptions = Options.Instance,
            };
            server.Init(db);
            var handler = server.GetHandler();
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

            var modifiedBook = new
            {
                Title = 1
            };
            var content = JsonContent.Create(modifiedBook);
            var response = await client.PatchAsync($"Books/{book.Id}", content);
            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [TestMethod]
        public async Task Invalid_field_name()
        {
            var db = new BookDbContext();
            var baseAddress = new Uri("http://localhost/api/");
            var server = new EfRestServer(baseAddress)
            {
                CloudCqsOptions = Options.Instance,
            };
            server.Init(db);
            var handler = server.GetHandler();
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

            var modifiedBook = new
            {
                x = 1
            };
            var content = JsonContent.Create(modifiedBook);
            var response = await client.PatchAsync($"Books/{book.Id}", content);
            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
