using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.OpenApi.Writers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EfRest.Swagger.Test
{
    [TestClass]
    public class SwaggerGenTest
    {
        [TestMethod]
        public void GenerateSwagger()
        {
            using var db = new BookDbContext();
            var jsonSerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            var swaggerGen = new EfRestSwagger(db, jsonSerializerOptions);
            var swagger = swaggerGen.GetSwagger();

            Assert.IsNotNull(swagger.Paths.FirstOrDefault(p => p.Key == "/publishers"));
        }
    }
}
