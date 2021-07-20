using System;
using System.Collections.Specialized;
using CloudCqs;
using CloudCqs.Facade;

namespace EfRest.Internal.DbContextHandler
{
    internal class DbContextHandlerFacade
        : Facade<DbContextHandlerFacade.Request, DbContextHandlerFacade.Response>
    {
        public record Repository(
            IQuery<DecodedUrlQuery.Request, DecodedUrlQuery.Response> DecodeUrlQuery);

        public record Request(string Path, string Query);
        public record Response(Type EntityType, string Resource, string? Id, NameValueCollection Param);

        public DbContextHandlerFacade(CloudCqsOptions option, Repository repository) : base(option)
        {
            var handler = new Handler()
                .Then($"Invoke {nameof(repository.DecodeUrlQuery)}", async props =>
                {
                    var (path, query) = props;
                    var request = new DecodedUrlQuery.Request(path, query);
                    var response = await repository.DecodeUrlQuery.Invoke(request);
                    return response;
                })
                .Then("Create result", props =>
                {
                    var (entityType, resource, id, param) = props;
                    return new Response(entityType, resource, id, param);
                })
                .Build();

            SetHandler(handler);
        }
    }
}
