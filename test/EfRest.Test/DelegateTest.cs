namespace EfRest.Test;

using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class DelegateTest
{
    [TestMethod]
    [Ignore("GitHub actionではexample.comに接続できない？")]
    public async Task Dont_fake()
    {
        var db = new BookDbContext();
        var baseAddress = new Uri("http://localhost/api/");
        var server = new EfRestServer(db) { CloudCqsOptions = Options.Instance, };
        var handler = new EfRestHandler(server, baseAddress);
        using var client = new HttpClient(handler);

        var response = await client.GetAsync("http://example.com");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }
}
