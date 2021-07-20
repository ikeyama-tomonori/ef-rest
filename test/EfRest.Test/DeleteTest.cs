using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EfRest.Test
{
    using Example.Model;

    [TestClass]
    public class DeleteTest
    {
        [TestMethod]
        public async Task Delete()
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

            var response = await client.DeleteAsync($"/Books/{book.Id}");
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            var deleted = await response.Content.ReadFromJsonAsync<Book>();
            Assert.AreEqual("New Book", deleted?.Title);
            Assert.IsNull(db.Books.FirstOrDefault(b => b.Title == "New Book"));
        }
    }
}
