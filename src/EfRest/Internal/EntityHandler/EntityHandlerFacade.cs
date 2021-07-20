using CloudCqs;
using CloudCqs.Facade;
using System;
using System.Collections.Specialized;
using System.Net;
using System.Net.Http;

namespace EfRest.Internal.EntityHandler
{
    internal class EntityHandlerFacade
        : Facade<EntityHandlerFacade.Request, HttpResponseMessage>
    {
        public record Repository(
            INewId<HttpContent, string> CreateNewId,
            ICommand<(string id, HttpContent content)> UpdateCommand,
            ICommand<(string id, HttpContent content)> PatchCommand,
            IQuery<(string id, NameValueCollection param), HttpResponseMessage> GetOneQuery,
            ICommand<string> DeleteCommand,
            IQuery<NameValueCollection, HttpResponseMessage> GetListQuery);

        public record Request(
            Repository Repository,
            HttpMethod Method,
            string Resource,
            string? Id,
            NameValueCollection Param,
            HttpContent? Content);

        public EntityHandlerFacade(CloudCqsOptions option) : base(option)
        {
            var handler = new Handler()
                .Then($"Invoke {nameof(Repository.CreateNewId)}", async props =>
                {
                    var (repository, method, resource, id, param, content) = props;
                    if (method == HttpMethod.Post && content != null)
                    {
                        var response = await repository.CreateNewId.Invoke(content);
                        var newId = response;
                        var location = new Uri($"/{resource}/{newId}", UriKind.Relative);
                        return (repository, method, id: newId, param, content, location);
                    }
                    return (repository, method, id, param, content, location: default(Uri));
                })
                .Then($"Invoke {nameof(Repository.UpdateCommand)}", async props =>
                {
                    var (repository, method, id, param, content, location) = props;
                    if (method == HttpMethod.Put && id != null && content != null)
                    {
                        await repository.UpdateCommand.Invoke((id, content));
                    }
                    return (repository, method, id, param, content, location);
                })
                .Then($"Invoke {nameof(Repository.PatchCommand)}", async props =>
                {
                    var (repository, method, id, param, content, location) = props;
                    if (method == HttpMethod.Patch && id != null && content != null)
                    {
                        await repository.PatchCommand.Invoke((id, content));
                    }
                    return (repository, method, id, param, location);
                })
                .Then($"Invoke {nameof(Repository.GetOneQuery)}", async props =>
                {
                    var (repository, method, id, param, location) = props;
                    if (id != null)
                    {
                        var response = await repository.GetOneQuery.Invoke((id, param));
                        if (location != null)
                        {
                            response.StatusCode = HttpStatusCode.Created;
                            response.Headers.Location = location;
                        }
                        return (repository, method, id, httpResponse: response, param);
                    }
                    return (repository, method, id, httpResponse: default(HttpResponseMessage), param);
                })
                .Then($"Invoke {nameof(Repository.DeleteCommand)}", async props =>
                {
                    var (repository, method, id, httpResponse, param) = props;
                    if (method == HttpMethod.Delete && id != null)
                    {
                        await repository.DeleteCommand.Invoke(id);
                    }
                    return (repository, method, httpResponse, param);
                })
                .Then($"Invoke {nameof(Repository.GetListQuery)}", async props =>
                {
                    var (repository, method, httpResponse, param) = props;
                    if (httpResponse != null) return httpResponse;
                    else if (method == HttpMethod.Get)
                    {
                        var response = await repository.GetListQuery.Invoke(param);
                        return response;
                    }
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);
                })
                .Build();

            SetHandler(handler);
        }
    }
}
