using System.Net;
using System.Net.Http.Headers;
using BTSS.IAR.Api.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("Sql")
        ?? throw new InvalidOperationException("Missing connection string: ConnectionStrings:Sql");

    options.UseSqlServer(connectionString);
});

// Browser-facing HttpClient for components/pages.
// Calls stay on the web host and are proxied from here to the API.
builder.Services.AddScoped(sp =>
{
    var nav = sp.GetRequiredService<NavigationManager>();
    return new HttpClient { BaseAddress = new Uri(nav.BaseUri) };
});

// Outbound proxy client to the API host.
var apiBaseUrl = builder.Configuration["Api:BaseUrl"]
    ?? throw new InvalidOperationException("Missing configuration: Api:BaseUrl");

builder.Services.AddHttpClient("ApiProxy", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl, UriKind.Absolute);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    AllowAutoRedirect = false,
    UseCookies = false
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

// Primary proxy surface: /api/* -> API /api/*
app.Map("/api/{**path}", ProxyToApiAsync);

// Compatibility surface: /admin/* -> API /api/admin/*
// This preserves current UI calls like "/admin/dashboard".
app.Map("/admin/{**path}", ProxyAdminCompatAsync);

app.Run();

async Task ProxyToApiAsync(HttpContext context)
{
    var relativePath = context.Request.Path.Value ?? "/api";
    await ProxyRequestAsync(context, relativePath);
}

async Task ProxyAdminCompatAsync(HttpContext context)
{
    var suffix = context.Request.Path.Value ?? "/admin";
    var forwardedPath = "/api" + suffix;
    await ProxyRequestAsync(context, forwardedPath);
}

async Task ProxyRequestAsync(HttpContext context, string targetPath)
{
    var clientFactory = context.RequestServices.GetRequiredService<IHttpClientFactory>();
    var client = clientFactory.CreateClient("ApiProxy");

    var targetUri = BuildTargetUri(client.BaseAddress!, targetPath, context.Request.QueryString);

    using var requestMessage = CreateProxyHttpRequest(context, targetUri);

    using var responseMessage = await client.SendAsync(
        requestMessage,
        HttpCompletionOption.ResponseHeadersRead,
        context.RequestAborted);

    await CopyProxyHttpResponse(context, responseMessage);
}

static Uri BuildTargetUri(Uri baseAddress, string path, QueryString queryString)
{
    var builder = new UriBuilder(new Uri(baseAddress, path))
    {
        Query = queryString.HasValue ? queryString.Value!.TrimStart('?') : string.Empty
    };

    return builder.Uri;
}

static HttpRequestMessage CreateProxyHttpRequest(HttpContext context, Uri targetUri)
{
    var requestMessage = new HttpRequestMessage
    {
        Method = new HttpMethod(context.Request.Method),
        RequestUri = targetUri
    };

    if (HttpMethods.IsPost(context.Request.Method) ||
        HttpMethods.IsPut(context.Request.Method) ||
        HttpMethods.IsPatch(context.Request.Method) ||
        HttpMethods.IsDelete(context.Request.Method))
    {
        requestMessage.Content = new StreamContent(context.Request.Body);
    }

    foreach (var header in context.Request.Headers)
    {
        if (ShouldSkipRequestHeader(header.Key))
            continue;

        if (!requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()) &&
            requestMessage.Content is not null)
        {
            requestMessage.Content.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
        }
    }

    // Forward original host/proto info so API can build correct redirects/links if needed.
    requestMessage.Headers.TryAddWithoutValidation("X-Forwarded-Host", context.Request.Host.Value);
    requestMessage.Headers.TryAddWithoutValidation("X-Forwarded-Proto", context.Request.Scheme);
    requestMessage.Headers.TryAddWithoutValidation("X-Forwarded-For", context.Connection.RemoteIpAddress?.ToString());

    return requestMessage;
}

static async Task CopyProxyHttpResponse(HttpContext context, HttpResponseMessage responseMessage)
{
    context.Response.StatusCode = (int)responseMessage.StatusCode;

    foreach (var header in responseMessage.Headers)
    {
        if (string.Equals(header.Key, HeaderNames.TransferEncoding, StringComparison.OrdinalIgnoreCase))
            continue;

        if (string.Equals(header.Key, HeaderNames.Location, StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Headers[header.Key] = RewriteLocationHeader(header.Value);
            continue;
        }

        context.Response.Headers[header.Key] = new StringValues(header.Value.ToArray());
    }

    foreach (var header in responseMessage.Content.Headers)
    {
        if (string.Equals(header.Key, HeaderNames.TransferEncoding, StringComparison.OrdinalIgnoreCase))
            continue;

        context.Response.Headers[header.Key] = new StringValues(header.Value.ToArray());
    }

    // Let Kestrel manage transfer encoding.
    context.Response.Headers.Remove(HeaderNames.TransferEncoding);

    if (responseMessage.Content is not null)
    {
        await responseMessage.Content.CopyToAsync(context.Response.Body);
    }
}

static string[] RewriteLocationHeader(IEnumerable<string> values)
{
    return values
        .Select(v =>
        {
            if (string.IsNullOrWhiteSpace(v))
                return v;

            // If API redirects to /api/... keep it local to the web host.
            if (Uri.TryCreate(v, UriKind.Absolute, out var absolute))
                return absolute.PathAndQuery + absolute.Fragment;

            return v;
        })
        .ToArray();
}

static bool ShouldSkipRequestHeader(string headerName)
{
    return headerName.Equals(HeaderNames.Host, StringComparison.OrdinalIgnoreCase)
        || headerName.Equals(HeaderNames.ContentLength, StringComparison.OrdinalIgnoreCase);
}