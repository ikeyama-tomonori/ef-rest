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
    public class EmbedTest
    {
        public Genre[] Genres { get; }

        public EmbedTest()
        {
            Genres = new[]
            {
                new Genre
                {
                    Name = "Level1",
                    ChildGenres = new[]
                    {
                        new Genre
                        {
                            Name = "Level2",
                            ChildGenres = new[]
                            {
                                new Genre
                                {
                                    Name = "Level3"
                                }
                            },
                            Books = new []
                            {
                                new Book
                                {
                                    Title = "Book Title",
                                }
                            }
                        }
                    }
                }
            };
        }

        [TestMethod]
        public async Task Single_value()
        {
            var jsonSerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                ReferenceHandler = ReferenceHandler.Preserve
            };

            using var db = new BookDbContext();
            await db.Genres.AddRangeAsync(Genres);
            await db.SaveChangesAsync();

            using var handler = new EfRestHandler(db)
            {
                JsonSerializerOptions = jsonSerializerOptions
            };
            using var client = new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost")
            };

            var embed = HttpUtility.UrlEncode(JsonSerializer.Serialize(new[] { "ParentGenre" }));
            var response = await client.GetFromJsonAsync<Genre[]>($"/Genres?embed={embed}", jsonSerializerOptions);
            var level3 = response?.Single(g => g.Name == "Level3");
            Assert.IsNotNull(level3?.ParentGenre);
        }

        [TestMethod]
        public async Task Json_name()
        {
            var jsonSerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                ReferenceHandler = ReferenceHandler.Preserve
            };

            using var db = new BookDbContext();
            await db.Genres.AddRangeAsync(Genres);
            await db.SaveChangesAsync();

            using var handler = new EfRestHandler(db)
            {
                JsonSerializerOptions = jsonSerializerOptions
            };
            using var client = new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost")
            };

            var embed = HttpUtility.UrlEncode(JsonSerializer.Serialize(new[] { "child_genres" }));
            var response = await client.GetFromJsonAsync<Genre[]>($"/Genres?embed={embed}", jsonSerializerOptions);
            var level3 = response?.Single(g => g.Name == "Level3");
            Assert.IsNotNull(level3?.ChildGenres);
        }

        [TestMethod]
        public async Task Json_path_name()
        {
            var jsonSerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                ReferenceHandler = ReferenceHandler.Preserve
            };

            using var db = new BookDbContext();
            await db.Genres.AddRangeAsync(Genres);
            await db.SaveChangesAsync();

            using var handler = new EfRestHandler(db)
            {
                JsonSerializerOptions = jsonSerializerOptions
            };
            using var client = new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost")
            };

            var embed = HttpUtility.UrlEncode(JsonSerializer.Serialize(new[] { "child_genres.child_genres" }));
            var response = await client.GetFromJsonAsync<Genre[]>($"/Genres?embed={embed}", jsonSerializerOptions);
            var level1 = response?.Single(g => g.Name == "Level1");
            Assert.IsNotNull(level1?.ChildGenres?.First().ChildGenres);
        }

        [TestMethod]
        public async Task Multiple()
        {
            var jsonSerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                ReferenceHandler = ReferenceHandler.Preserve
            };

            using var db = new BookDbContext();
            await db.Genres.AddRangeAsync(Genres);
            await db.SaveChangesAsync();

            using var handler = new EfRestHandler(db)
            {
                JsonSerializerOptions = jsonSerializerOptions
            };
            using var client = new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost")
            };

            var embed = HttpUtility.UrlEncode(
                JsonSerializer.Serialize(
                    new[]
                    {
                        "ParentGenre",
                        "child_genres.child_genres",
                        "Books"
                    }));
            var response = await client.GetFromJsonAsync<Genre[]>($"/Genres?embed={embed}", jsonSerializerOptions);
            var level2 = response?.Single(g => g.Name == "Level2");
            Assert.IsNotNull(level2?.ParentGenre);
            Assert.IsNotNull(level2?.ChildGenres?.First().ChildGenres);
            Assert.IsNotNull(level2?.Books?.First().Title);
        }

        [TestMethod]
        public async Task Json_invalid()
        {
            using var db = new BookDbContext();
            await db.Genres.AddRangeAsync(Genres);
            await db.SaveChangesAsync();

            using var handler = new EfRestHandler(db);
            using var client = new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost")
            };

            var embed = HttpUtility.UrlEncode("{}");
            var response = await client.GetAsync($"Genres?embed={embed}");
            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [TestMethod]
        public async Task Json_array_invalid()
        {
            using var db = new BookDbContext();
            await db.Genres.AddRangeAsync(Genres);
            await db.SaveChangesAsync();

            using var handler = new EfRestHandler(db);
            using var client = new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost")
            };

            var embed = HttpUtility.UrlEncode(JsonSerializer.Serialize(new[] { 1 }));
            var response = await client.GetAsync($"Genres?embed={embed}");
            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [TestMethod]
        public async Task Name_invalid()
        {
            using var db = new BookDbContext();
            await db.Genres.AddRangeAsync(Genres);
            await db.SaveChangesAsync();

            using var handler = new EfRestHandler(db);
            using var client = new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost")
            };

            var embed = HttpUtility.UrlEncode(JsonSerializer.Serialize(new[] { "xxx" }));
            var response = await client.GetAsync($"Genres?embed={embed}");
            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
