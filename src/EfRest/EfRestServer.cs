using System;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CloudCqs;
using Microsoft.EntityFrameworkCore;

namespace EfRest
{
    using Internal;

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
            var repository = GetRepository(name);
            var response = await repository.GetListQuery.Invoke((param, cancellationToken));
            return response;
        }

        public async Task<HttpResponseMessage> GetOne(string name, string id, NameValueCollection param, CancellationToken cancellationToken = default)
        {
            var repository = GetRepository(name);
            var response = await repository.GetOneQuery.Invoke((id, param, cancellationToken));
            return response;
        }

        public async Task<HttpResponseMessage> AddOne(string name, string item, CancellationToken cancellationToken = default)
        {
            var repository = GetRepository(name);
            var id = await repository.CreateNewId.Invoke((item, cancellationToken));
            var response = await repository.GetOneQuery.Invoke((id, new(), cancellationToken));
            response.StatusCode = HttpStatusCode.Created;
            response.Headers.Location = new Uri($"{BaseAddress.AbsolutePath}{name}/{id}", UriKind.Relative);
            return response;
        }

        public async Task<HttpResponseMessage> UpdateOne(string name, string id, string item, CancellationToken cancellationToken = default)
        {
            var repository = GetRepository(name);
            await repository.UpdateCommand.Invoke((id, item, cancellationToken));
            var response = await repository.GetOneQuery.Invoke((id, new(), cancellationToken));
            return response;
        }

        public async Task<HttpResponseMessage> RemoveOne(string name, string id, CancellationToken cancellationToken = default)
        {
            var repository = GetRepository(name);
            var response = await repository.GetOneQuery.Invoke((id, new(), cancellationToken));
            await repository.DeleteCommand.Invoke((id, cancellationToken));
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

        private EntityTypedRepository GetRepository(string name)
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
            var createEntityHandlerRepository = GetType()
                .GetMethod(
                    nameof(CreateEntityTypedRepository),
                    BindingFlags.InvokeMethod
#pragma warning disable S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields
                    | BindingFlags.NonPublic
#pragma warning restore S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields
                    | BindingFlags.Instance)?
                .MakeGenericMethod(entityType);
            if (createEntityHandlerRepository == null)
            {
                throw new NullGuardException(nameof(createEntityHandlerRepository));
            }
            var result = createEntityHandlerRepository.Invoke(this, null);
            if (result is EntityTypedRepository repository)
            {
                return repository;
            }
            throw new TypeGuardException(typeof(EntityTypedRepository), result);
        }

        private EntityTypedRepository CreateEntityTypedRepository<TEntity>()
            where TEntity : class
        {
            if (DbContext == null) throw new NullGuardException(nameof(DbContext));
            return new(
                new CreateNewId<TEntity>(CloudCqsOptions, DbContext, JsonSerializerOptions),
                new UpdateCommand<TEntity>(CloudCqsOptions, DbContext, JsonSerializerOptions),
                new GetOneQuery<TEntity>(CloudCqsOptions, DbContext, JsonSerializerOptions),
                new DeleteCommand<TEntity>(CloudCqsOptions, DbContext, JsonSerializerOptions),
                new GetListQuery<TEntity>(CloudCqsOptions, DbContext, JsonSerializerOptions));
        }

    }
}
