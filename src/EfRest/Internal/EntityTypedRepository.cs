using System.Collections.Specialized;
using System.Net.Http;
using System.Threading;
using CloudCqs;

namespace EfRest.Internal
{
    internal record EntityTypedRepository(
        INewId<(string content, CancellationToken cancellationToken), string> CreateNewId,
        ICommand<(string id, string content, CancellationToken cancellationToken)> PatchCommand,
        IQuery<(string id, NameValueCollection param, CancellationToken cancellationToken), HttpResponseMessage> GetOneQuery,
        ICommand<(string id, CancellationToken cancellationToken)> DeleteCommand,
        IQuery<(NameValueCollection param, CancellationToken cancellationToken), HttpResponseMessage> GetListQuery);
}
