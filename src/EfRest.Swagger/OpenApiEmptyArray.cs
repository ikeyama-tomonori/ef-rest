using System;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Writers;

namespace EfRest.Swagger;

internal class OpenApiEmptyArray : IOpenApiPrimitive
{
    public AnyType AnyType => AnyType.Primitive;

    public PrimitiveType PrimitiveType => throw new NotImplementedException();

    public void Write(IOpenApiWriter writer, OpenApiSpecVersion specVersion)
    {
        writer.WriteStartArray();
        writer.WriteEndArray();
    }
}

