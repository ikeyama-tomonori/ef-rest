using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CloudCqs;
using EfRest.Extensions;
using EfRest.Internal;
using Microsoft.EntityFrameworkCore;

namespace EfRest;

public class EfRestServer
{
    public JsonSerializerOptions JsonSerializerOptions { get; init; } = new(JsonSerializerDefaults.Web);
    public CloudCqsOptions CloudCqsOptions { get; init; } = new();

    public DbContext DbContext { get; set; }

    public EfRestServer(DbContext dbContext)
    {
        DbContext = dbContext;
    }

    public async Task<(int statusCode, Dictionary<string, string> headers, string body)> GetAll(string name, NameValueCollection param, CancellationToken cancellationToken = default)
    {
        var repository = GetRepositoryFactory(name);
        var request = (embed: param["embed"], filter: param["filter"], sort: param["sort"], range: param["range"]);
        var (json, total, range) = await repository.GetListFacade().Invoke(request, cancellationToken);

        if (range is (var first, var last))
        {
            var headers = new Dictionary<string, string>
            {
                ["Content-Range"] = $"items {first}-{last}/{total}"
            };
            var statusCode = total == last - first + 1
                ? (int)HttpStatusCode.OK
                : (int)HttpStatusCode.PartialContent;

            return (statusCode, headers, body: json);
        }
        else
        {
            var headers = new Dictionary<string, string>
            {
                ["Content-Range"] = $"items */{total}"
            };
            var statusCode = (int)HttpStatusCode.PartialContent;

            return (statusCode, headers, body: json);
        }
    }

    public async Task<(int statusCode, Dictionary<string, string> headers, string body)>
        GetOne(string name, string id, NameValueCollection param, CancellationToken cancellationToken = default)
    {
        var repository = GetRepositoryFactory(name);
        var json = await repository.GetOneFacade().Invoke((id, param["embed"]), cancellationToken);
        var statusCode = (int)HttpStatusCode.OK;

        return (statusCode, headers: new(), body: json);
    }

    public async Task<(int statusCode, Dictionary<string, string> headers, string body)>
        AddOne(string name, string item, CancellationToken cancellationToken = default)
    {
        var repository = GetRepositoryFactory(name);
        var id = await repository.CreateFacade().Invoke(item, cancellationToken);
        var response = await GetOne(name, id, new(), cancellationToken);
        var headers = new Dictionary<string, string>
        {
            ["Location"] = $"/{name}/{id}"
        };
        var statusCode = (int)HttpStatusCode.Created;

        return (statusCode, headers, response.body);
    }

    public async Task<(int statusCode, Dictionary<string, string> headers, string body)>
        UpdateOne(string name, string id, string item, CancellationToken cancellationToken = default)
    {
        var repository = GetRepositoryFactory(name);
        await repository.UpdateFacade().Invoke((id, item), cancellationToken);
        var response = await GetOne(name, id, new(), cancellationToken);
        return response;
    }

    public async Task<(int statusCode, Dictionary<string, string> headers, string body)>
        PatchOne(string name, string id, string item, CancellationToken cancellationToken = default)
    {
        var repository = GetRepositoryFactory(name);
        await repository.PatchFacade().Invoke((id, item), cancellationToken);
        var response = await GetOne(name, id, new(), cancellationToken);
        return response;
    }

    public async Task<(int statusCode, Dictionary<string, string> headers, string body)>
        RemoveOne(string name, string id, CancellationToken cancellationToken = default)
    {
        var repository = GetRepositoryFactory(name);
        var response = await GetOne(name, id, new(), cancellationToken);
        await repository.DeleteFacade().Invoke(id, cancellationToken);
        return response;
    }

    public async Task<(int statusCode, Dictionary<string, string> headers, string? body)>
        Request(string method, string pathAndQuery, string? body, CancellationToken cancellationToken = default)
    {
        if (DbContext == null)
        {
            return (statusCode: (int)HttpStatusCode.BadGateway, headers: new(), body: null);
        }

        try
        {
            var decodedUrlQuery = new DecodedUrlQuery(
                CloudCqsOptions,
                DbContext,
                JsonSerializerOptions);
            var (resource, id, param) = await decodedUrlQuery.Invoke(pathAndQuery, cancellationToken);

            var response = (method, id, body) switch
            {
                (method: "POST", id: null, body: string)
                    => await AddOne(resource, body, cancellationToken),
                (method: "GET", id: string, body: _)
                    => await GetOne(resource, id, param, cancellationToken),
                (method: "GET", id: null, body: _)
                    => await GetAll(resource, param, cancellationToken),
                (method: "PUT", id: string, body: string)
                    => await UpdateOne(resource, id, body, cancellationToken),
                (method: "PATCH", id: string, body: string)
                    => await PatchOne(resource, id, body, cancellationToken),
                (method: "DELETE", id: string, body: _)
                    => await RemoveOne(resource, id, cancellationToken),
                _ => (statusCode: (int)HttpStatusCode.NotFound, headers: new(), body: null)
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
        var propertyInfo = DbContext?
            .GetType()
            .GetPropertyInfoByJsonName(name, JsonSerializerOptions);

        if (propertyInfo == null
            || propertyInfo.PropertyType.GetGenericTypeDefinition() != typeof(DbSet<>))
        {
            throw new StatusCodeException(
                HttpStatusCode.NotFound,
                new($"Resource name not found: {name}", new[] { nameof(name) }));
        }
        var entityType = propertyInfo.PropertyType.GetGenericArguments().First();
        var getRepositoryFactoryWithKey = GetType()
            .GetMethod(
                nameof(GetRepositoryFactoryWithKey),
                BindingFlags.InvokeMethod
#pragma warning disable S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields
                    | BindingFlags.NonPublic
#pragma warning restore S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields
                    | BindingFlags.Instance)?
            .MakeGenericMethod(entityType);
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

    private RepositoryFactory GetRepositoryFactoryWithKey<TEntity>()
        where TEntity : class
    {
        if (DbContext == null) throw new NullGuardException(nameof(DbContext));
        var dbSet = DbContext.Set<TEntity>();
        var keyType = dbSet
            .EntityType
            .FindPrimaryKey()?
            .Properties
            .SingleOrDefault()?
            .PropertyInfo?
            .PropertyType;
        if (keyType == null)
        {
            throw new StatusCodeException(
                HttpStatusCode.BadRequest,
                new($"Entity must have single primary key.", new[] { "resource" }));
        }
        var createRepositoryFactory = GetType()
            .GetMethod(
                nameof(CreateRepositoryFactory),
                BindingFlags.InvokeMethod
#pragma warning disable S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields
                | BindingFlags.NonPublic
#pragma warning restore S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields
                | BindingFlags.Instance)?
            .MakeGenericMethod(typeof(TEntity), keyType);
        if (createRepositoryFactory == null)
        {
            throw new NullGuardException(nameof(CreateRepositoryFactory));
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
        if (DbContext == null) throw new NullGuardException(nameof(DbContext));
        return new(
            CreateFacade: () => new CreateFacade<TEntity, TKey>(
                option: CloudCqsOptions,
                repository: (
                    jsonDeserializeQuery: new JsonDeserializeQuery<TEntity>(
                        CloudCqsOptions,
                        JsonSerializerOptions,
                        whenError: (e, json) => throw new StatusCodeException(
                            HttpStatusCode.BadRequest,
                            new($"{e.Message}: {json}", new[] { "body" }))),
                    createNewId: new CreateNewId<TEntity, TKey>(CloudCqsOptions, DbContext),
                    jsonSerializeQuery: new JsonSerializeQuery<TKey>(CloudCqsOptions, JsonSerializerOptions))),

            GetListFacade: () => new GetListFacade<TEntity>(
                option: CloudCqsOptions,
                repository: (
                    getListQuery: new GetListQuery<TEntity>(CloudCqsOptions, DbContext, JsonSerializerOptions),
                    jsonSerializeQuery: new JsonSerializeQuery<TEntity[]>(CloudCqsOptions, JsonSerializerOptions))),

            GetOneFacade: () => new GetOneFacade<TEntity, TKey>(
                option: CloudCqsOptions,
                repository: (

                    jsonDeserializeQuery: new JsonDeserializeQuery<TKey>(
                        CloudCqsOptions,
                        JsonSerializerOptions,
                        whenError: (e, json) => throw new StatusCodeException(
                            HttpStatusCode.NotFound,
                            new($"{e.Message}: {json}", new[] { "id" }))),
                    getOneQuery: new GetOneQuery<TEntity, TKey>(CloudCqsOptions, DbContext, JsonSerializerOptions),
                    jsonSerializeQuery: new JsonSerializeQuery<TEntity>(CloudCqsOptions, JsonSerializerOptions))),

            UpdateFacade: () => new UpdateFacade<TEntity, TKey>(
                option: CloudCqsOptions,
                repository: (
                    keyDeserializeQuery: new JsonDeserializeQuery<TKey>(
                        CloudCqsOptions,
                        JsonSerializerOptions,
                        whenError: (e, json) => throw new StatusCodeException(
                            HttpStatusCode.NotFound,
                            new($"{e.Message}: {json}", new[] { "id" }))),
                    entityDeserializeQuery: new JsonDeserializeQuery<TEntity>(
                        CloudCqsOptions,
                        JsonSerializerOptions,
                        whenError: (e, json) => throw new StatusCodeException(
                            HttpStatusCode.BadRequest,
                            new($"{e.Message}: {json}", new[] { "body" }))),
                    updateCommand: new UpdateCommand<TEntity, TKey>(CloudCqsOptions, DbContext))),

            PatchFacade: () => new PatchFacade<TKey>(
                option: CloudCqsOptions,
                repository: (
                    keyDeserializeQuery: new JsonDeserializeQuery<TKey>(
                        CloudCqsOptions,
                        JsonSerializerOptions,
                        whenError: (e, json) => throw new StatusCodeException(
                            HttpStatusCode.NotFound,
                            new($"{e.Message}: {json}", new[] { "id" }))),
                    patchDeserializeQuery: new JsonDeserializeQuery<JsonElement>(
                        CloudCqsOptions,
                        JsonSerializerOptions,
                        whenError: (e, json) => throw new StatusCodeException(
                            HttpStatusCode.BadRequest,
                            new($"{e.Message}: {json}", new[] { "body" }))),
                    patchCommand: new PatchCommand<TEntity, TKey>(CloudCqsOptions, DbContext, JsonSerializerOptions))),

           DeleteFacade: () => new DeleteFacade<TKey>(
               option: CloudCqsOptions,
               repository: (
                   jsonDeserializeQuery: new JsonDeserializeQuery<TKey>(
                       CloudCqsOptions,
                       JsonSerializerOptions,
                       whenError: (e, json) => throw new StatusCodeException(
                            HttpStatusCode.NotFound,
                            new($"{e.Message}: {json}", new[] { "id" }))),
                   deleteCommand: new DeleteCommand<TEntity, TKey>(CloudCqsOptions, DbContext))));
    }
}
