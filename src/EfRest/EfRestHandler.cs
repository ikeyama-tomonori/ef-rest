namespace EfRest;

using System.Net;

public class EfRestHandler : DelegatingHandler
{
    private readonly EfRestServer server;
    private readonly Uri baseAddress;

    public EfRestHandler(EfRestServer server, Uri baseAddress) : base(new HttpClientHandler())
    {
        this.server = server;
        this.baseAddress = baseAddress;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        if (request.RequestUri == null || !this.baseAddress.IsBaseOf(request.RequestUri))
        {
            return await base.SendAsync(request, cancellationToken);
        }

        var uri = request.RequestUri;
        var relative = this.baseAddress.MakeRelativeUri(uri);
        var pathAndQuery = relative.OriginalString;
        var requestBody =
            request.Content == null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

        var (statusCode, headers, body) = await this.server.Request(
            request.Method.Method,
            pathAndQuery,
            requestBody,
            cancellationToken
        );

        var content = body == null ? null : new StringContent(body, System.Text.Encoding.UTF8);
        var httpResponse = new HttpResponseMessage((HttpStatusCode)statusCode)
        {
            Content = content,
        };

        foreach (var (key, value) in headers)
        {
            if (key == "Content-Range")
            {
                content?.Headers.Add(key, value);
            }
            else if (key == "Location")
            {
                var basePath = this.baseAddress.AbsolutePath;
                if (basePath.Length == 0)
                {
                    httpResponse.Headers.Add(key, value);
                }
                else
                {
                    httpResponse.Headers.Add(key, basePath[..^1] + value);
                }
            }
            else
            {
                httpResponse.Headers.Add(key, value);
            }
        }

        return httpResponse;
    }
}
