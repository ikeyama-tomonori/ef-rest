using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EfRest.Test
{
    [TestClass]
    public class DelegateTest
    {
        [TestMethod]
        public async Task Dont_fake()
        {
            var db = new BookDbContext();
            var baseAddress = new Uri("http://localhost/api/");
            var server = new EfRestServer(baseAddress)
            {
                CloudCqsOptions = Options.Instance,
            };
            server.Init(db);
            var handler = server.GetHandler();
            using var client = new HttpClient(handler);

            var response = await client.GetAsync("http://example.com");
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
