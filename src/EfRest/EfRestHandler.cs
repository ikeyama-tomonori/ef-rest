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
    using Internal.DbContextHandler;
    using Internal.EntityHandler;

    public sealed class EfRestHandler : HttpMessageHandler
    {
        public DbContext DbContext { get; }
        public JsonSerializerOptions JsonSerializerOptions { get; init; } = new(JsonSerializerDefaults.Web);
        public CloudCqsOptions CloudCqsOptions { get; init; } = new();

        private IFacade<DbContextHandlerFacade.Request, DbContextHandlerFacade.Response>
            DbContextHandlerFacade
        { get; }

        private IFacade<EntityHandlerFacade.Request, HttpResponseMessage>
            EntityHandlerFacade
        { get; }

        public EfRestHandler(DbContext dbContext)
        {
            DbContext = dbContext;

            DbContextHandlerFacade = new DbContextHandlerFacade(
                CloudCqsOptions,
                new(new DecodedUrlQuery(CloudCqsOptions, DbContext, JsonSerializerOptions)));
            EntityHandlerFacade = new EntityHandlerFacade(CloudCqsOptions);
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            try
            {
                DbContext.ChangeTracker.Clear();
                await DbContext.Database.BeginTransactionAsync(cancellationToken);

                if (request.RequestUri == null) throw new NullGuardException(nameof(request.RequestUri));

                var (entityType, resource, id, param) = await DbContextHandlerFacade.Invoke(new(
                    request.RequestUri.AbsolutePath,
                    request.RequestUri.Query));

                var createEntityHandlerRepository = GetType()
                    .GetMethod(
                        nameof(CreateEntityHandlerRepository),
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
                if (result is EntityHandlerFacade.Repository repository)
                {
                    var response = await EntityHandlerFacade.Invoke(new(
                        repository,
                        request.Method,
                        resource,
                        id,
                        param,
                        request.Content));

                    await DbContext.Database.CommitTransactionAsync(cancellationToken);

                    return response;
                }
                throw new TypeGuardException(typeof(EntityHandlerFacade.Repository), result);
            }
            catch (StatusCodeException exception)
            {
                var content = JsonContent.Create(exception.Object, null, JsonSerializerOptions);
                var response = new HttpResponseMessage(exception.HttpStatusCode)
                {
                    Content = content
                };
                return response;
            }
        }

        private EntityHandlerFacade.Repository CreateEntityHandlerRepository<TEntity>()
            where TEntity : class
            => new(
                new CreateNewId<TEntity>(CloudCqsOptions, DbContext, JsonSerializerOptions),
                new UpdateCommand<TEntity>(CloudCqsOptions, DbContext, JsonSerializerOptions),
                new PatchCommand<TEntity>(CloudCqsOptions, DbContext, JsonSerializerOptions),
                new GetOneQuery<TEntity>(CloudCqsOptions, DbContext, JsonSerializerOptions),
                new DeleteCommand<TEntity>(CloudCqsOptions, DbContext, JsonSerializerOptions),
                new GetListQuery<TEntity>(CloudCqsOptions, DbContext, JsonSerializerOptions));
    }
}
