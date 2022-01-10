using System;
using System.Collections.Specialized;
using System.Linq;
using System.Text.Json;
using System.Web;
using CloudCqs;
using CloudCqs.Query;
using Microsoft.EntityFrameworkCore;

namespace EfRest.Internal;

internal class DecodedUrlQuery : Query<
        Uri,
        (string resource, string? id, NameValueCollection param)>
{
    public DecodedUrlQuery(
        CloudCqsOptions option,
        DbContext db,
        JsonSerializerOptions jsonSerializerOptions,
        Uri baseAddress) : base(option)
    {
        var handler = new Handler()
            .Then("Decode path and query string", p =>
            {
                var uri = p;
                var relative = baseAddress.MakeRelativeUri(uri);
                var pathAndQuery = relative.OriginalString;
                var pathQueryArray = pathAndQuery.Split('?');
                var path = pathQueryArray[0];
                var query = pathQueryArray.Length == 1 ? "" : pathQueryArray[1];
                var param = HttpUtility.ParseQueryString(query);
                return (path, param);
            })
            .Then("Split path into resource and id nominee", p =>
            {
                var (path, param) = p;
                var index = path.LastIndexOf("/");
                var nominee = new
                {
                    resource = index == -1 ? path : path[0..index],
                    id =
                        index == -1 || index == path.Length - 1
                        ? null
                        : path[(index + 1)..]
                };
                return (nominee, param);
            })
            .Then("Handle resource name include slash.", p =>
             {
                 var (nominee, param) = p;
                 var resourceNameIncludeSlash =
                    nominee.id == null
                    ? null
                    : $"{nominee.resource}/{nominee.id}";
                 var resourceNameIncludeSlashPropInfo =
                    resourceNameIncludeSlash == null
                    ? null
                    : db
                        .GetType()
                        .GetPropertyInfoByJsonName(
                            resourceNameIncludeSlash,
                            jsonSerializerOptions);
                 var entityTypeIncludeSlash = resourceNameIncludeSlashPropInfo?
                        .PropertyType
                        .GetGenericArguments()
                        .First();
                 var includeSlash =
                    (resourceNameIncludeSlash == null || entityTypeIncludeSlash == null)
                    ? null
                    : resourceNameIncludeSlash;

                 return (includeSlash, nominee, param);
             })
            .Then("Handle resouce name without slash.", p =>
            {
                var (includeSlash, nominee, param) = p;
                if (includeSlash != null)
                {
                    return (name: includeSlash, null, param);
                }

                var entityPropertyInfo = db
                        .GetType()
                        .GetPropertyInfoByJsonName(
                           nominee.resource,
                           jsonSerializerOptions);
                if (entityPropertyInfo == null)
                {
                    throw new NotFoundException(new()
                    {
                        ["path"] = new[] { $"Resource name not found: {nominee.resource}" }
                    });
                }

                return (name: nominee.resource, nominee.id, param);
            })
            .Build();

        SetHandler(handler);
    }
}
