namespace EfRest;

using System.Collections.Specialized;
using System.Net;
using System.Reflection;
using System.Text.Json;
using CloudCqs;
using EfRest.Extensions;
using EfRest.Internal;
using Microsoft.EntityFrameworkCore;

public class EfRestServer
{
    public EfRestServer(DbContext dbContext)
    {
        this.DbContext = dbContext;
    }

    public JsonSerializerOptions JsonSerializerOptions { get; init; } =
        new(JsonSerializerDefaults.Web);
    public CloudCqsOptions CloudCqsOptions { get; init; } = new();

    public DbContext DbContext { get; set; }

    public async Task<(int StatusCode, Dictionary<string, string> Headers, string Body)> GetAll(
        string name,
        NameValueCollection param,
        CancellationToken cancellationToken = default
    )
    {
        var repository = this.GetRepositoryFactory(name);
        var request = (
            embed: param["embed"],
            filter: param["filter"],
            sort: param["sort"],
            range: param["range"]
        );
        var (json, total, range) = await repository
            .GetListFacade()
            .Invoke(request, cancellationToken);

        if (range is (var first, var last))
        {
            var headers = new Dictionary<string, string>
            {
                ["Content-Range"] = $"items {first}-{last}/{total}",
            };
            var statusCode =
                total == last - first + 1
                    ? (int)HttpStatusCode.OK
                    : (int)HttpStatusCode.PartialContent;

            return (statusCode, headers, Body: json);
        }
        else
        {
            var headers = new Dictionary<string, string> { ["Content-Range"] = $"items */{total}" };
            var statusCode = (int)HttpStatusCode.PartialContent;

            return (statusCode, headers, Body: json);
        }
    }

    public async Task<(int StatusCode, Dictionary<string, string> Headers, string Body)> GetOne(
        string name,
        string id,
        NameValueCollection param,
        CancellationToken cancellationToken = default
    )
    {
        var repository = this.GetRepositoryFactory(name);
        var json = await repository.GetOneFacade().Invoke((id, param["embed"]), cancellationToken);
        var statusCode = (int)HttpStatusCode.OK;

        return (statusCode, Headers: new(), Body: json);
    }

    public async Task<(int StatusCode, Dictionary<string, string> Headers, string Body)> AddOne(
        string name,
        string item,
        CancellationToken cancellationToken = default
    )
    {
        var repository = this.GetRepositoryFactory(name);
        var id = await repository.CreateFacade().Invoke(item, cancellationToken);
        var (_, _, body) = await this.GetOne(name, id, new(), cancellationToken);
        var headers = new Dictionary<string, string> { ["Location"] = $"/{name}/{id}" };
        var statusCode = (int)HttpStatusCode.Created;

        return (statusCode, headers, body);
    }

    public async Task<(int StatusCode, Dictionary<string, string> Headers, string Body)> UpdateOne(
        string name,
        string id,
        string item,
        CancellationToken cancellationToken = default
    )
    {
        var repository = this.GetRepositoryFactory(name);
        await repository.UpdateFacade().Invoke((id, item), cancellationToken);
        var response = await this.GetOne(name, id, new(), cancellationToken);
        return response;
    }

    public async Task<(int StatusCode, Dictionary<string, string> Headers, string Body)> PatchOne(
        string name,
        string id,
        string item,
        CancellationToken cancellationToken = default
    )
    {
        var repository = this.GetRepositoryFactory(name);
        await repository.PatchFacade().Invoke((id, item), cancellationToken);
        var response = await this.GetOne(name, id, new(), cancellationToken);
        return response;
    }

    public async Task<(int StatusCode, Dictionary<string, string> Headers, string Body)> RemoveOne(
        string name,
        string id,
        CancellationToken cancellationToken = default
    )
    {
        var repository = this.GetRepositoryFactory(name);
        var response = await this.GetOne(name, id, new(), cancellationToken);
        await repository.DeleteFacade().Invoke(id, cancellationToken);
        return response;
    }

    public async Task<(int StatusCode, Dictionary<string, string> Headers, string? Body)> Request(
        string method,
        string pathAndQuery,
        string? body,
        CancellationToken cancellationToken = default
    )
    {
        if (this.DbContext == null)
        {
            return (StatusCode: (int)HttpStatusCode.BadGateway, Headers: new(), Body: null);
        }

        try
        {
            var decodedUrlQuery = new DecodedUrlQuery(
                this.CloudCqsOptions,
                this.DbContext,
                this.JsonSerializerOptions
            );
            var (resource, id, param) = await decodedUrlQuery.Invoke(
                pathAndQuery,
                cancellationToken
            );

            var response = (method, id, body) switch
            {
                (method: "POST", id: null, body: string)
                    => await this.AddOne(resource, body, cancellationToken),
                (method: "GET", id: string, body: _)
                    => await this.GetOne(resource, id, param, cancellationToken),
                (method: "GET", id: null, body: _)
                    => await this.GetAll(resource, param, cancellationToken),
                (method: "PUT", id: string, body: string)
                    => await this.UpdateOne(resource, id, body, cancellationToken),
                (method: "PATCH", id: string, body: string)
                    => await this.PatchOne(resource, id, body, cancellationToken),
                (method: "DELETE", id: string, body: _)
                    => await this.RemoveOne(resource, id, cancellationToken),
                _ => (StatusCode: (int)HttpStatusCode.NotFound, Headers: new(), Body: null),
            };

            return response;
        }
        catch (StatusCodeException exception)
        {
            var responseBody = exception.ValidationResult.ErrorMessage;
            var statusCode = exception.HttpStatusCode;

            return ((int)statusCode, new(), responseBody);
        }
    }

    private RepositoryFactory GetRepositoryFactory(string name)
    {
        var propertyInfo = this.DbContext
            ?.GetType()
            .GetPropertyInfoByJsonName(name, this.JsonSerializerOptions);

        if (
            propertyInfo == null
            || propertyInfo.PropertyType.GetGenericTypeDefinition() != typeof(DbSet<>)
        )
        {
            throw new StatusCodeException(
                HttpStatusCode.NotFound,
                new($"Resource name not found: {name}", new[] { nameof(name) })
            );
        }
        var entityType = propertyInfo.PropertyType.GetGenericArguments().First();
        var getRepositoryFactoryWithKey = this.GetType()
            .GetMethod(
                nameof(this.GetRepositoryFactoryWithKey),
                BindingFlags.InvokeMethod
#pragma warning disable S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields
                    | BindingFlags.NonPublic
#pragma warning restore S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields
                    | BindingFlags.Instance
            )
            ?.MakeGenericMethod(entityType);
        if (getRepositoryFactoryWithKey == null)
        {
            throw new NullGuardException(nameof(getRepositoryFactoryWithKey));
        }
        var result = getRepositoryFactoryWithKey.Invoke(this, null);
        if (result is RepositoryFactory repository)
        {
            return repository;
        }
        throw new TypeGuardException(typeof(RepositoryFactory), result);
    }

    private RepositoryFactory GetRepositoryFactoryWithKey<TEntity>() where TEntity : class
    {
        if (this.DbContext == null)
        {
            throw new NullGuardException(nameof(this.DbContext));
        }
        var dbSet = this.DbContext.Set<TEntity>();
        var keyType = dbSet.EntityType
            .FindPrimaryKey()
            ?.Properties.SingleOrDefault()
            ?.PropertyInfo?.PropertyType;
        if (keyType == null)
        {
            throw new StatusCodeException(
                HttpStatusCode.BadRequest,
                new($"Entity must have single primary key.", new[] { "resource" })
            );
        }
        var createRepositoryFactory = this.GetType()
            .GetMethod(
                nameof(this.CreateRepositoryFactory),
                BindingFlags.InvokeMethod
#pragma warning disable S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields
                    | BindingFlags.NonPublic
#pragma warning restore S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields
                    | BindingFlags.Instance
            )
            ?.MakeGenericMethod(typeof(TEntity), keyType);
        if (createRepositoryFactory == null)
        {
            throw new NullGuardException(nameof(this.CreateRepositoryFactory));
        }
        var result = createRepositoryFactory.Invoke(this, null);
        if (result is RepositoryFactory repository)
        {
            return repository;
        }
        throw new TypeGuardException(typeof(RepositoryFactory), result);
    }

    private RepositoryFactory CreateRepositoryFactory<TEntity, TKey>()
        where TEntity : class
        where TKey : notnull
    {
        if (this.DbContext == null)
        {
            throw new NullGuardException(nameof(this.DbContext));
        }
        return new(
            CreateFacade: () =>
                new CreateFacade<TEntity, TKey>(
                    option: this.CloudCqsOptions,
                    repository: (
                        JsonDeserializeQuery: new JsonDeserializeQuery<TEntity>(
                            this.CloudCqsOptions,
                            this.JsonSerializerOptions,
                            whenError: (e, json) =>
                                throw new StatusCodeException(
                                    HttpStatusCode.BadRequest,
                                    new($"{e.Message}: {json}", new[] { "body" })
                                )
                        ),
                        CreateNewId: new CreateNewId<TEntity, TKey>(
                            this.CloudCqsOptions,
                            this.DbContext
                        ),
                        JsonSerializeQuery: new JsonSerializeQuery<TKey>(
                            this.CloudCqsOptions,
                            this.JsonSerializerOptions
                        )
                    )
                ),
            GetListFacade: () =>
                new GetListFacade<TEntity>(
                    option: this.CloudCqsOptions,
                    repository: (
                        GetListQuery: new GetListQuery<TEntity>(
                            this.CloudCqsOptions,
                            this.DbContext,
                            this.JsonSerializerOptions
                        ),
                        JsonSerializeQuery: new JsonSerializeQuery<TEntity[]>(
                            this.CloudCqsOptions,
                            this.JsonSerializerOptions
                        )
                    )
                ),
            GetOneFacade: () =>
                new GetOneFacade<TEntity, TKey>(
                    option: this.CloudCqsOptions,
                    repository: (
                        JsonDeserializeQuery: new JsonDeserializeQuery<TKey>(
                            this.CloudCqsOptions,
                            this.JsonSerializerOptions,
                            whenError: (e, json) =>
                                throw new StatusCodeException(
                                    HttpStatusCode.NotFound,
                                    new($"{e.Message}: {json}", new[] { "id" })
                                )
                        ),
                        GetOneQuery: new GetOneQuery<TEntity, TKey>(
                            this.CloudCqsOptions,
                            this.DbContext,
                            this.JsonSerializerOptions
                        ),
                        JsonSerializeQuery: new JsonSerializeQuery<TEntity>(
                            this.CloudCqsOptions,
                            this.JsonSerializerOptions
                        )
                    )
                ),
            UpdateFacade: () =>
                new UpdateFacade<TEntity, TKey>(
                    option: this.CloudCqsOptions,
                    repository: (
                        KeyDeserializeQuery: new JsonDeserializeQuery<TKey>(
                            this.CloudCqsOptions,
                            this.JsonSerializerOptions,
                            whenError: (e, json) =>
                                throw new StatusCodeException(
                                    HttpStatusCode.NotFound,
                                    new($"{e.Message}: {json}", new[] { "id" })
                                )
                        ),
                        EntityDeserializeQuery: new JsonDeserializeQuery<TEntity>(
                            this.CloudCqsOptions,
                            this.JsonSerializerOptions,
                            whenError: (e, json) =>
                                throw new StatusCodeException(
                                    HttpStatusCode.BadRequest,
                                    new($"{e.Message}: {json}", new[] { "body" })
                                )
                        ),
                        UpdateCommand: new UpdateCommand<TEntity, TKey>(
                            this.CloudCqsOptions,
                            this.DbContext
                        )
                    )
                ),
            PatchFacade: () =>
                new PatchFacade<TKey>(
                    option: this.CloudCqsOptions,
                    repository: (
                        KeyDeserializeQuery: new JsonDeserializeQuery<TKey>(
                            this.CloudCqsOptions,
                            this.JsonSerializerOptions,
                            whenError: (e, json) =>
                                throw new StatusCodeException(
                                    HttpStatusCode.NotFound,
                                    new($"{e.Message}: {json}", new[] { "id" })
                                )
                        ),
                        PatchDeserializeQuery: new JsonDeserializeQuery<JsonElement>(
                            this.CloudCqsOptions,
                            this.JsonSerializerOptions,
                            whenError: (e, json) =>
                                throw new StatusCodeException(
                                    HttpStatusCode.BadRequest,
                                    new($"{e.Message}: {json}", new[] { "body" })
                                )
                        ),
                        PatchCommand: new PatchCommand<TEntity, TKey>(
                            this.CloudCqsOptions,
                            this.DbContext,
                            this.JsonSerializerOptions
                        )
                    )
                ),
            DeleteFacade: () =>
                new DeleteFacade<TKey>(
                    option: this.CloudCqsOptions,
                    repository: (
                        JsonDeserializeQuery: new JsonDeserializeQuery<TKey>(
                            this.CloudCqsOptions,
                            this.JsonSerializerOptions,
                            whenError: (e, json) =>
                                throw new StatusCodeException(
                                    HttpStatusCode.NotFound,
                                    new($"{e.Message}: {json}", new[] { "id" })
                                )
                        ),
                        DeleteCommand: new DeleteCommand<TEntity, TKey>(
                            this.CloudCqsOptions,
                            this.DbContext
                        )
                    )
                )
        );
    }
}
