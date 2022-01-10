using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.OpenApi.Writers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EfRest.Swagger.Test;

[TestClass]
public class SwaggerGenTest
{
    [TestMethod]
    public void GenerateSwagger()
    {
        using var db = new BookDbContext();
        var jsonSerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var swaggerGen = new EfRestSwagger(db, jsonSerializerOptions);
        var swagger = swaggerGen.GetSwagger(
            documentName: "test",
            documentVersion: "v1",
            host: "http://localhost:4000",
            basePath: "/api");

        Assert.IsNotNull(swagger.Paths.FirstOrDefault(p => p.Key == "/publishers"));

        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream);
        var jsonWriter = new OpenApiJsonWriter(writer);
        swagger.SerializeAsV3(jsonWriter);

        stream.Position = 0;
        var reader = new StreamReader(stream);
        var buf = reader.ReadToEnd();
        Assert.IsTrue(buf.Any());
    }
}

