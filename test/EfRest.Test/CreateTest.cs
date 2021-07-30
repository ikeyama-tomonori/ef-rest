using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EfRest.Test
{
    using Example.Model;

    [TestClass]
    public class CreateTest
    {
        [TestMethod]
        public async Task Create()
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

            var newBook = new Book
            {
                Title = "New Book"
            };
            var response = await client.PostAsJsonAsync("Books", newBook);
            var content = await response.Content.ReadFromJsonAsync<Book>();

            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
            Assert.AreEqual("New Book", content?.Title);

            var createdBook = await db.Books.SingleAsync(b => b.Title == "New Book");
            Assert.AreEqual($"/api/Books/{createdBook.Id}", response.Headers.GetValues("Location").First());
            Assert.AreEqual("New Book", createdBook.Title);
        }

        [TestMethod]
        public async Task Invalid_json()
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

            var response = await client.PostAsJsonAsync("Books", "xxx");
            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [TestMethod]
        public async Task Invalid_data()
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

            var newBook = new
            {
                xxx = "New Book"
            };
            var response = await client.PostAsJsonAsync("Books", newBook);
            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [TestMethod]
        public async Task Ignore_id_field()
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

            var newBook = new Book
            {
                Title = "New Book"
            };
            await db.Books.AddAsync(newBook);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var response = await client.PostAsJsonAsync("Books", newBook);
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        }
    }
}
