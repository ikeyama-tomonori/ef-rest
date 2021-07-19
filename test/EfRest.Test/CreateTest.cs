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
    public class CreateTest
    {
        [TestMethod]
        public async Task Create()
        {
            using var db = new BookDbContext();
            using var handler = new EfRestHandler(db);
            using var client = new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost")
            };

            var newBook = new Book
            {
                Title = "New Book"
            };
            var response = await client.PostAsJsonAsync("/Books", newBook);
            var content = await response.Content.ReadFromJsonAsync<Book>();

            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
            Assert.AreEqual("New Book", content?.Title);

            var createdBook = await db.Books.SingleAsync(b => b.Title == "New Book");
            Assert.AreEqual($"/Books/{createdBook.Id}", response.Headers.GetValues("Location").First());
            Assert.AreEqual("New Book", createdBook.Title);
        }

        [TestMethod]
        public async Task Invalid_json()
        {
            using var db = new BookDbContext();
            using var handler = new EfRestHandler(db);
            using var client = new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost")
            };

            var response = await client.PostAsJsonAsync("/Books", "xxx");
            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [TestMethod]
        public async Task Invalid_data()
        {
            using var db = new BookDbContext();
            using var handler = new EfRestHandler(db);
            using var client = new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost")
            };

            var newBook = new
            {
                xxx = "New Book"
            };
            var response = await client.PostAsJsonAsync("/Books", newBook);
            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [TestMethod]
        public async Task Ignore_id_field()
        {
            using var db = new BookDbContext();
            using var handler = new EfRestHandler(db);
            using var client = new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost")
            };

            var newBook = new Book
            {
                Title = "New Book"
            };
            await db.Books.AddAsync(newBook);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var response = await client.PostAsJsonAsync("/Books", newBook);
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        }
    }
}
