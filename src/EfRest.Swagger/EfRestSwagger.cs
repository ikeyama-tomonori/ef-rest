using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace EfRest.Swagger
{
    public class EfRestSwagger
    {
        private readonly JsonSerializerOptions _jsonSerializerOptions;
        private readonly Dictionary<string, Type> _resourceTypes;

        public EfRestSwagger(DbContext db, JsonSerializerOptions jsonSerializerOptions)
        {
            _jsonSerializerOptions = jsonSerializerOptions;
            _resourceTypes = db
                .GetType()
                .GetProperties()
                .Where(pi => pi.PropertyType.IsGenericType
                    && pi.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
                .Select(pi =>
                {
                    var type = pi.PropertyType.GetGenericArguments().First();
                    var jsonPropertyNameAttribute = pi.GetCustomAttribute<JsonPropertyNameAttribute>();
                    var name =
                        jsonPropertyNameAttribute == null
                        ? _jsonSerializerOptions.PropertyNamingPolicy?
                            .ConvertName(pi.Name)
                            ?? pi.Name
                        : jsonPropertyNameAttribute.Name;
                    return (name, type);
                })
                .ToDictionary(t => t.name, t => t.type);
        }

        public OpenApiDocument GetSwagger(string documentName, string documentVersion, string? host = null, string? basePath = null)
        {
            var entities = _resourceTypes;
            var generatorOptions = new SchemaGeneratorOptions();
            var serializerDataContractResolver = new JsonSerializerDataContractResolver(_jsonSerializerOptions);
            var schemaGenerator = new SchemaGenerator(generatorOptions, serializerDataContractResolver);
            var schemaRepository = new SchemaRepository();

            var pathItems = entities.Select(entity =>
            {
                var schema = schemaGenerator.GenerateSchema(entity.Value, schemaRepository);
                var tags = new[] { new OpenApiTag { Name = entity.Key } };
                var pathItem = new OpenApiPathItem
                {
                    Operations =
                    {
                        [OperationType.Post] = new()
                        {
                            Tags = tags,
                            OperationId = $"{entity.Key}_Create",
                            RequestBody = new()
                            {
                                Content =
                                {
                                    ["application/json"] = new ()
                                        {
                                            Schema = schema
                                        }
                                }
                            },
                            Responses = new()
                            {
                                [$"{(int)HttpStatusCode.Created}"] = new OpenApiResponse
                                {
                                    Description = $"{HttpStatusCode.Created}",
                                    Content =
                                    {
                                        ["application/json"] = new ()
                                        {
                                            Schema = schema
                                        }
                                    }
                                }
                            }
                        },
                        [OperationType.Get] = new()
                        {
                            Tags = tags,
                            OperationId = $"{entity.Key}_GetList",
                            Parameters =
                            {
                                new()
                                {
                                    Name = "filter",
                                    In = ParameterLocation.Query,
                                    Content =
                                    {
                                        ["application/json"] = new ()
                                        {
                                            Schema = new()
                                            {
                                                Type = "object"
                                            }
                                        }
                                    }
                                },
                                new()
                                {
                                    Name = "sort",
                                    In = ParameterLocation.Query,
                                    Content =
                                    {
                                        ["application/json"] = new ()
                                        {
                                            Schema = new()
                                            {
                                                Type = "array",
                                                Items = new()
                                                {
                                                    Type = "string"
                                                }
                                            },
                                            Example = new OpenApiArray
                                            {
                                                new OpenApiString("id"),
                                                new OpenApiString("asc"),
                                            }
                                        }
                                    }
                                },
                                new()
                                {
                                    Name = "range",
                                    In = ParameterLocation.Query,
                                    Content =
                                    {
                                        ["application/json"] = new ()
                                        {
                                            Schema = new()
                                            {
                                                Type = "array",
                                                Items = new()
                                                {
                                                    Type = "integer",
                                                    Format = "int32"

                                                }
                                            },
                                            Example = new OpenApiArray
                                            {
                                                new OpenApiInteger(0),
                                                new OpenApiInteger(9),
                                            }
                                        }
                                    }
                                },
                                new()
                                {
                                    Name = "embed",
                                    In = ParameterLocation.Query,
                                    Content =
                                    {
                                        ["application/json"] = new ()
                                        {
                                            Schema = new()
                                            {
                                                Type = "array",
                                                Items = new()
                                                {
                                                    Type = "string"
                                                }
                                            },
                                            Example = new OpenApiEmptyArray()
                                        }
                                    }
                                },
                            },
                            Responses = new()
                            {
                                [$"{(int)HttpStatusCode.OK}"] = new OpenApiResponse
                                {
                                    Description = $"{HttpStatusCode.OK}",
                                    Content =
                                    {
                                        ["application/json"] = new ()
                                        {
                                            Schema = new()
                                            {
                                                Type = "array",
                                                Items = schema
                                            }
                                        }
                                    }
                                },
                                [$"{(int)HttpStatusCode.PartialContent}"] = new OpenApiResponse
                                {
                                    Description = $"{HttpStatusCode.PartialContent}",
                                    Content =
                                    {
                                        ["application/json"] = new ()
                                        {
                                            Schema = new()
                                            {
                                                Type = "array",
                                                Items = schema
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                };

                var pathItemWithId = new OpenApiPathItem
                {
                    Operations =
                    {
                        [OperationType.Get] = new()
                        {
                            Tags = tags,
                            OperationId = $"{entity.Key}_GetOne",
                            Parameters =
                            {
                                new()
                                {
                                    Name = "id",
                                    In = ParameterLocation.Path,
                                    Required = true,
                                    Schema = schemaGenerator.GenerateSchema(typeof(int), schemaRepository),
                                },
                                new()
                                {
                                    Name = "embed",
                                    In = ParameterLocation.Query,
                                    Content =
                                    {
                                        ["application/json"] = new ()
                                        {
                                            Schema = new()
                                            {
                                                Type = "array",
                                                Items = new()
                                                {
                                                    Type = "string"
                                                }
                                            },
                                            Example = new OpenApiEmptyArray()
                                        }
                                    }
                                },
                            },
                            Responses = new()
                            {
                                [$"{(int)HttpStatusCode.OK}"] = new OpenApiResponse
                                {
                                    Description = $"{HttpStatusCode.OK}",
                                    Content =
                                    {
                                        ["application/json"] = new ()
                                        {
                                            Schema = schema
                                        }
                                    }
                                }
                            }
                        },
                        [OperationType.Put] = new()
                        {
                            Tags = tags,
                            OperationId = $"{entity.Key}_Update",
                            Parameters =
                            {
                                new()
                                {
                                    Name = "id",
                                    In = ParameterLocation.Path,
                                    Required = true,
                                    Schema = schemaGenerator.GenerateSchema(typeof(int), schemaRepository),
                                }
                            },
                            RequestBody = new()
                            {
                                Content =
                                {
                                    ["application/json"] = new ()
                                        {
                                            Schema = schema
                                        }
                                }
                            },
                            Responses = new()
                            {
                                [$"{(int)HttpStatusCode.OK}"] = new OpenApiResponse
                                {
                                    Description = $"{HttpStatusCode.OK}",
                                    Content =
                                    {
                                        ["application/json"] = new ()
                                        {
                                            Schema = schema
                                        }
                                    }
                                }
                            }
                        },
                        [OperationType.Patch] = new()
                        {
                            Tags = tags,
                            OperationId = $"{entity.Key}_Patch",
                            Parameters =
                            {
                                new()
                                {
                                    Name = "id",
                                    In = ParameterLocation.Path,
                                    Required = true,
                                    Schema = schemaGenerator.GenerateSchema(typeof(int), schemaRepository),
                                }
                            },
                            RequestBody = new()
                            {
                                Content =
                                {
                                    ["application/json"] = new ()
                                        {
                                            Schema = new OpenApiSchema
                                            {
                                                Type = "object"
                                            }
                                        }
                                }
                            },
                            Responses = new()
                            {
                                [$"{(int)HttpStatusCode.OK}"] = new OpenApiResponse
                                {
                                    Description = $"{HttpStatusCode.OK}",
                                    Content =
                                    {
                                        ["application/json"] = new ()
                                        {
                                            Schema = schema
                                        }
                                    }
                                }
                            }
                        },
                        [OperationType.Delete] = new()
                        {
                            Tags = tags,
                            OperationId = $"{entity.Key}_Delete",
                            Parameters =
                            {
                                new()
                                {
                                    Name = "id",
                                    In = ParameterLocation.Path,
                                    Required = true,
                                    Schema = schemaGenerator.GenerateSchema(typeof(int), schemaRepository),
                                }
                            },
                            Responses = new()
                            {
                                [$"{(int)HttpStatusCode.OK}"] = new OpenApiResponse
                                {
                                    Description = $"{HttpStatusCode.OK}",
                                    Content =
                                    {
                                        ["application/json"] = new ()
                                        {
                                            Schema = schema
                                        }
                                    }
                                }
                            }
                        }
                    }
                };
                return (entity.Key, pathItem, pathItemWithId);
            });

            var paths = new OpenApiPaths();
            foreach (var (name, pathItem, pathItemWithId) in pathItems)
            {
                paths.Add($"/{name}", pathItem);
                paths.Add($"/{name}/{{id}}", pathItemWithId);
            }

            var swaggerDoc = new OpenApiDocument
            {
                Info = new()
                {
                    Title = documentName,
                    Version = documentVersion
                },
                Servers =
                    host == null && basePath == null
                    ? Array.Empty<OpenApiServer>()
                    : new[] { new OpenApiServer { Url = $"{host}{basePath}" } },
                Paths = paths,
                Components = new OpenApiComponents
                {
                    Schemas = schemaRepository.Schemas
                },

            };

            return swaggerDoc;
        }
    }
}
