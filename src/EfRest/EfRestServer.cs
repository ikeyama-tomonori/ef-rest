﻿using System;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CloudCqs;
using EfRest.Internal;
using Microsoft.EntityFrameworkCore;

namespace EfRest;

public class EfRestServer
{
    public Uri BaseAddress { get; }

    public JsonSerializerOptions JsonSerializerOptions { get; init; } = new(JsonSerializerDefaults.Web);
    public CloudCqsOptions CloudCqsOptions { get; init; } = new();

    public DbContext? DbContext { get; set; }

    public EfRestServer(Uri baseAddress)
    {
        BaseAddress = baseAddress;
    }

    public void Init(DbContext dbContext)
    {
        DbContext = dbContext;
    }

    public async Task<HttpResponseMessage> GetAll(string name, NameValueCollection param, CancellationToken cancellationToken = default)
    {
        var repository = GetRepositoryFactory(name);
        var request = (embed: param["embed"], filter: param["filter"], sort: param["sort"], range: param["range"]);
        var (json, total, range) = await repository.GetListFacade(cancellationToken).Invoke(request);
        var contentRange = range is (var first, var last)
            ? new ContentRangeHeaderValue(first, last, total)
            : new ContentRangeHeaderValue(total);
        contentRange.Unit = "items";

        var status = contentRange.Length == contentRange.To - contentRange.From + 1
            ? HttpStatusCode.OK
            : HttpStatusCode.PartialContent;
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        content.Headers.ContentRange = contentRange;

        var responseMessage = new HttpResponseMessage(status)
        {
            Content = content,
        };

        return responseMessage;
    }

    public async Task<HttpResponseMessage> GetOne(string name, string id, NameValueCollection param, CancellationToken cancellationToken = default)
    {
        var repository = GetRepositoryFactory(name);
        var json = await repository.GetOneFacade(cancellationToken).Invoke((id, param["embed"]));
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = content
        };
        return response;
    }

    public async Task<HttpResponseMessage> AddOne(string name, string item, CancellationToken cancellationToken = default)
    {
        var repository = GetRepositoryFactory(name);
        var id = await repository.CreateFacade(cancellationToken).Invoke(item);
        var response = await GetOne(name, id, new(), cancellationToken);
        response.StatusCode = HttpStatusCode.Created;
        response.Headers.Location = new Uri($"{BaseAddress.AbsolutePath}{name}/{id}", UriKind.Relative);
        return response;
    }

    public async Task<HttpResponseMessage> UpdateOne(string name, string id, string item, CancellationToken cancellationToken = default)
    {
        var repository = GetRepositoryFactory(name);
        await repository.UpdateCommand(cancellationToken).Invoke((id, item));
        var response = await GetOne(name, id, new(), cancellationToken);
        return response;
    }

    public async Task<HttpResponseMessage> RemoveOne(string name, string id, CancellationToken cancellationToken = default)
    {
        var repository = GetRepositoryFactory(name);
        var response = await GetOne(name, id, new(), cancellationToken);
        await repository.DeleteCommand(cancellationToken).Invoke(id);
        return response;
    }

    public class Handler : DelegatingHandler
    {
        private readonly EfRestServer _server;

        internal Handler(EfRestServer server) : base(new HttpClientHandler())
        {
            _server = server;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri == null || !_server.BaseAddress.IsBaseOf(request.RequestUri))
            {
                return await base.SendAsync(request, cancellationToken);
            }

            if (_server.DbContext == null)
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            }

            var decodedUrlQuery = new DecodedUrlQuery(
                _server.CloudCqsOptions,
                _server.DbContext,
                _server.JsonSerializerOptions,
                _server.BaseAddress);

            try
            {
                var (resource, id, param) = await decodedUrlQuery.Invoke(request.RequestUri);
                var item =
                    request.Content == null
                    ? null
                    : await request.Content.ReadAsStringAsync(cancellationToken);

                if (id == null && request.Method == HttpMethod.Get)
                {
                    return await _server.GetAll(resource, param, cancellationToken);
                }
                else if (id == null && request.Method == HttpMethod.Post && item != null)
                {
                    return await _server.AddOne(resource, item, cancellationToken);
                }
                else if (id != null && request.Method == HttpMethod.Get)
                {
                    return await _server.GetOne(resource, id, param, cancellationToken);
                }
                else if (id != null
                    && (request.Method == HttpMethod.Put || request.Method == HttpMethod.Patch)
                    && item != null)
                {
                    return await _server.UpdateOne(resource, id, item, cancellationToken);
                }
                else if (id != null && request.Method == HttpMethod.Delete)
                {
                    return await _server.RemoveOne(resource, id, cancellationToken);
                }
                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            }
            catch (StatusCodeException exception)
            {
                var content = JsonContent.Create(exception.Object, null, _server.JsonSerializerOptions);
                var response = new HttpResponseMessage(exception.HttpStatusCode)
                {
                    Content = content
                };
                return response;
            }

        }
    }

    public Handler GetHandler()
    {
        return new(this);
    }

    private RepositoryFactory GetRepositoryFactory(string name)
    {
        var propertyInfo = DbContext?
            .GetType()
            .GetPropertyInfoByJsonName(name, JsonSerializerOptions);

        if (propertyInfo == null
            || propertyInfo.PropertyType.GetGenericTypeDefinition() != typeof(DbSet<>))
        {
            throw new NotFoundException(new()
            {
                { nameof(name), new[] { $"Resource name not found: {name}" } }
            });
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
            throw new BadRequestException(new()
            {
                ["resource"] = new[] { $"Entity must have single primary key." }
            });
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
            cancellationToken => new CreateFacade<TEntity, TKey>(option: CloudCqsOptions,
                repository: (new JsonDeserializeQuery<TEntity>(CloudCqsOptions, JsonSerializerOptions),
                            new CreateNewId<TEntity, TKey>(CloudCqsOptions, DbContext, cancellationToken),
                            new JsonSerializeQuery<TKey>(CloudCqsOptions, JsonSerializerOptions))),

            cancellationToken => new UpdateCommand<TEntity>(CloudCqsOptions, DbContext, JsonSerializerOptions, cancellationToken),

            cancellationToken => new GetOneFacade<TEntity, TKey>(option: CloudCqsOptions,
                repository: (new JsonDeserializeQuery<TKey>(CloudCqsOptions, JsonSerializerOptions),
                            new GetOneQuery<TEntity, TKey>(CloudCqsOptions, DbContext, JsonSerializerOptions, cancellationToken),
                            new JsonSerializeQuery<TEntity>(CloudCqsOptions, JsonSerializerOptions))),

            cancellationToken => new DeleteCommand<TEntity>(CloudCqsOptions, DbContext, JsonSerializerOptions, cancellationToken),

            cancellationToken => new GetListFacade<TEntity>(option: CloudCqsOptions,
                repository: (new GetListQuery<TEntity>(CloudCqsOptions, DbContext, JsonSerializerOptions, cancellationToken),
                            new JsonSerializeQuery<TEntity[]>(CloudCqsOptions, JsonSerializerOptions))));
    }
}
