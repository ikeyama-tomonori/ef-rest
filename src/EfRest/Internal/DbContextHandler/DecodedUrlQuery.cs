using CloudCqs;
using CloudCqs.Query;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Text.Json;
using System.Reflection;
using System.Web;
using System.Collections.Specialized;

namespace EfRest.Internal.DbContextHandler
{
    internal class DecodedUrlQuery : Query<DecodedUrlQuery.Request, DecodedUrlQuery.Response>
    {
        public record Request(string Path, string Query);
        public record Response(Type EntityType, string Resource, string? Id, NameValueCollection Param);

        public DecodedUrlQuery(CloudCqsOptions option, DbContext db, JsonSerializerOptions jsonSerializerOptions) : base(option)
        {
            var handler = new Handler()
                .Then("Decode query string", props =>
                {
                    var (path, query) = props;
                    var param = HttpUtility.ParseQueryString(query);
                    return (path, param);
                })
                .Then("Split path into resource and id nominee", props =>
                {
                    var (path, param) = props;
                    var pathWithoutRoot = path[1..];
                    var index = pathWithoutRoot.LastIndexOf("/");
                    var nominee = new
                    {
                        resource = index == -1 ? pathWithoutRoot : pathWithoutRoot[0..index],
                        id = index == -1 || index == pathWithoutRoot.Length - 1
                            ? null
                            : pathWithoutRoot[(index + 1)..]
                    };
                    return (nominee, param);
                })
                .Then("Handle resource name include slash.", props =>
                 {
                     var (nominee, param) = props;
                     var resourceNameIncludeSlash =
                        nominee.id == null
                        ? null
                        : $"{nominee.resource}/{nominee.id}";
                     var resourceNameIncludeSlashPropInfo =
                        resourceNameIncludeSlash == null
                        ? null
                        : db
                            .GetType()
                            .GetPropertyInfo(
                                 resourceNameIncludeSlash,
                                jsonSerializerOptions);
                     var entityTypeIncludeSlash = resourceNameIncludeSlashPropInfo?
                            .PropertyType
                            .GetGenericArguments()
                            .First();
                     (string name, Type type)? includeSlash =
                        (resourceNameIncludeSlash == null || entityTypeIncludeSlash == null)
                        ? null
                        : (name: resourceNameIncludeSlash, type: entityTypeIncludeSlash);

                     return (includeSlash, nominee, param);
                 })
                .Then("Handle resouce name without slash.", props =>
                {
                    var (includeSlash, nominee, param) = props;
                    if (includeSlash is (string name, Type type))
                    {
                        return new Response(type, name, null, param);
                    }

                    var entityPropertyInfo = db
                            .GetType()
                            .GetPropertyInfo(
                               nominee.resource,
                               jsonSerializerOptions);
                    if (entityPropertyInfo == null) throw new NullGuardException(nameof(entityPropertyInfo));
                    var entityType = entityPropertyInfo.PropertyType.GetGenericArguments().First();

                    return new Response(entityType, nominee.resource, nominee.id, param);
                })
                .Build();

            SetHandler(handler);
        }
    }
}
