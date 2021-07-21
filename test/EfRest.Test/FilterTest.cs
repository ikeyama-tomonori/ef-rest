using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EfRest.Test
{
    using Example.Model;

    [TestClass]
    public class FilterTest
    {
        public Book[] Books { get; }

        public FilterTest()
        {
            Books = new[]
            {
                new Book
                {
                    Title = "Anna Karenina",
                    Description = "####XXX#####",
                    BookDetail = new()
                    {
                        TotalPages = 1000,
                        Rating = 2.0m,
                    }
                },
                new Book
                {
                    Title = "War and Peace",
                    BookDetail = new()
                    {
                        TotalPages = 2000,
                        Rating = 2.5m,
                        Publisher =new ()
                        {
                            Name = "Publisher1"
                        }
                    }
                },
                new Book
                {
                    Title = "Pride and Prejudice",
                    BookDetail = new()
                    {
                        Rating = 3.0m,
                    }
                },
                new Book
                {
                    Title = "Sense and Sensibility",
                    BookDetail = new()
                    {
                        TotalPages = 1000,
                        Rating = 3.5m,
                    }
                },
            };
        }

        [TestMethod]
        public async Task Full_text()
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

            await db.Books.AddRangeAsync(Books);
            await db.SaveChangesAsync();

            var filter = HttpUtility.UrlEncode(JsonSerializer.Serialize(new { Q = "XXX" }));
            var response = await client.GetFromJsonAsync<Book[]>($"Books?filter={filter}");
            Assert.AreEqual("Anna Karenina", response?.Single().Title);
        }

        [TestMethod]
        public async Task Equal_string()
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

            await db.Books.AddRangeAsync(Books);
            await db.SaveChangesAsync();

            var filter = HttpUtility.UrlEncode(JsonSerializer.Serialize(new { Title = "War and Peace" }));
            var response = await client.GetFromJsonAsync<Book[]>($"Books?filter={filter}");
            Assert.AreEqual("War and Peace", response?.Single().Title);
        }

        [TestMethod]
        public async Task Equal_int()
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

            await db.Books.AddRangeAsync(Books);
            await db.SaveChangesAsync();

            var id = Books.Single(b => b.Title == "War and Peace").BookDetail?.Publisher?.Id;
            var filter = HttpUtility.UrlEncode(JsonSerializer.Serialize(new { PublisherId = id }));
            var response = await client.GetFromJsonAsync<BookDetail[]>($"books/details/?filter={filter}");
            Assert.AreEqual(id, response?.Single().PublisherId);
        }

        [TestMethod]
        public async Task Contains()
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

            await db.Books.AddRangeAsync(Books);
            await db.SaveChangesAsync();

            var filter = HttpUtility.UrlEncode(
                JsonSerializer.Serialize(
                    new
                    {
                        Title = new[] { "Anna Karenina", "War and Peace", }
                    }));
            var response = await client.GetFromJsonAsync<Book[]>($"Books?filter={filter}");
            Assert.AreEqual(2, response?.Length);
        }

        private record NullValueFilter(int? PublisherId = null);

        [TestMethod]
        public async Task Null()
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

            await db.Books.AddRangeAsync(Books);
            await db.SaveChangesAsync();

            var filter = HttpUtility.UrlEncode(JsonSerializer.Serialize(new NullValueFilter()));
            var response = await client.GetFromJsonAsync<BookDetail[]>($"books/details/?filter={filter}");
            Assert.IsNotNull(response);
            Assert.IsNull(response?.First().PublisherId);
        }

        [TestMethod]
        public async Task Greater_than()
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

            await db.Books.AddRangeAsync(Books);
            await db.SaveChangesAsync();

            var filter = HttpUtility.UrlEncode(JsonSerializer.Serialize(new { Rating_gt = 3.0m }));
            var response = await client.GetFromJsonAsync<BookDetail[]>($"books/details/?filter={filter}");
            Assert.AreEqual(3.5m, response?.Single().Rating);
        }

        [TestMethod]
        public async Task Greater_than_or_equal()
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

            await db.Books.AddRangeAsync(Books);
            await db.SaveChangesAsync();

            var filter = HttpUtility.UrlEncode(JsonSerializer.Serialize(new { Rating_gte = 3.5m }));
            var response = await client.GetFromJsonAsync<BookDetail[]>($"books/details/?filter={filter}");
            Assert.AreEqual(3.5m, response?.Single().Rating);
        }

        [TestMethod]
        public async Task Less_than_or_equal()
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

            await db.Books.AddRangeAsync(Books);
            await db.SaveChangesAsync();

            var filter = HttpUtility.UrlEncode(JsonSerializer.Serialize(new { Rating_lte = 2.0m }));
            var response = await client.GetFromJsonAsync<BookDetail[]>($"books/details/?filter={filter}");
            Assert.AreEqual(2.0m, response?.Single().Rating);
        }

        [TestMethod]
        public async Task Less_than()
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

            await db.Books.AddRangeAsync(Books);
            await db.SaveChangesAsync();

            var filter = HttpUtility.UrlEncode(JsonSerializer.Serialize(new { Rating_lt = 2.5m }));
            var response = await client.GetFromJsonAsync<BookDetail[]>($"books/details/?filter={filter}");
            Assert.AreEqual(2.0m, response?.Single().Rating);
        }

        [TestMethod]
        public async Task Not_equal()
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

            await db.Books.AddRangeAsync(Books);
            await db.SaveChangesAsync();

            var filter = HttpUtility.UrlEncode(JsonSerializer.Serialize(new { Rating_neq = 2.5m }));
            var response = await client.GetFromJsonAsync<BookDetail[]>($"books/details/?filter={filter}");
            Assert.IsFalse(response!.Any(bd => bd.Rating == 2.5m));
        }

        [TestMethod]
        public async Task Navigation_property()
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

            await db.Books.AddRangeAsync(Books);
            await db.SaveChangesAsync();

            var filter = HttpUtility.UrlEncode("{\"Book.Title\": \"Anna Karenina\"}");
            var response = await client.GetFromJsonAsync<BookDetail[]>($"books/details/?filter={filter}");
            Assert.IsFalse(response!.Any(bd => bd.Rating == 2.5m));
        }

        [TestMethod]
        public async Task Json_invalid()
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

            await db.Books.AddRangeAsync(Books);
            await db.SaveChangesAsync();

            var filter = HttpUtility.UrlEncode("{");
            var response = await client.GetAsync($"books/details/?filter={filter}");
            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [TestMethod]
        public async Task Json_not_object()
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

            await db.Books.AddRangeAsync(Books);
            await db.SaveChangesAsync();

            var filter = HttpUtility.UrlEncode("[1,2]");
            var response = await client.GetAsync($"books/details/?filter={filter}");
            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [TestMethod]
        public async Task Property_not_exists()
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

            await db.Books.AddRangeAsync(Books);
            await db.SaveChangesAsync();

            var filter = HttpUtility.UrlEncode(JsonSerializer.Serialize(new { xxx = "xxx" }));
            var response = await client.GetAsync($"books/details/?filter={filter}");
            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [TestMethod]
        public async Task Value_invalid()
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

            await db.Books.AddRangeAsync(Books);
            await db.SaveChangesAsync();

            var filter = HttpUtility.UrlEncode(JsonSerializer.Serialize(new { Rating = "xxx" }));
            var response = await client.GetAsync($"books/details/?filter={filter}");
            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [TestMethod]
        public async Task Complex_search()
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

            await db.Books.AddRangeAsync(Books);
            await db.SaveChangesAsync();

            var filter = HttpUtility.UrlEncode("{\"q\": \"Anna\", \"BookDetail.Rating_lte\": 2.0}");
            var response = await client.GetFromJsonAsync<Book[]>($"Books?filter={filter}");
            Assert.AreEqual("Anna Karenina", response?.Single().Title);
        }
    }
}
