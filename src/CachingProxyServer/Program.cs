// --- 1. using ディレクティブ (全てファイルの先頭に) ---
using System.Collections.Concurrent;
using System.CommandLine;
using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Primitives;
using System; // 基本的な型のために追加 (もし不足していた場合)
using System.IO; // ストリーム操作のために追加 (もし不足していた場合)
using System.Linq; // ConcatなどのLinqメソッドを使うため
using System.Threading.Tasks; // Task を使うため
using Microsoft.AspNetCore.Builder; // WebApplication を使うため
using Microsoft.AspNetCore.Http; // HttpRequest/Response を使うため
using Microsoft.Extensions.DependencyInjection; // AddHttpClient を使うため
using Microsoft.Extensions.Hosting; // WebApplication を使うため

// --- 3. トップレベルステートメント (型の宣言の後) ---

// コマンドライン引数の定義
var portOption = new Option<int>(
    name: "--port",
    description: "Port number for the proxy server.",
    getDefaultValue: () => 8080);

var originOption = new Option<Uri>(
    name: "--origin",
    description: "Origin server URL to forward requests to.",
    parseArgument: result =>
    {
        string? uriString = result.Tokens.SingleOrDefault()?.Value;
        if (string.IsNullOrEmpty(uriString) || !Uri.TryCreate(uriString, UriKind.Absolute, out var uri))
        {
            result.ErrorMessage = $"Invalid origin URL: {uriString}";
            return null!;
        }
        return new Uri(uriString.TrimEnd('/'));
    })
{
    IsRequired = true
};

var rootCommand = new RootCommand("Starts a simple caching proxy server.");
rootCommand.AddOption(portOption);
rootCommand.AddOption(originOption);

// キャッシュを保持する ConcurrentDictionary
var cache = new ConcurrentDictionary<string, CachedResponse>();

// メインの処理
rootCommand.SetHandler(async (port, originUri) =>
{
    Console.WriteLine($"Starting caching proxy server on port {port} for origin {originUri}...");

    var builder = WebApplication.CreateBuilder(args); // argsは SetHandler の外のものを参照

    builder.Services.AddHttpClient("OriginClient");
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy =>
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader());
    });

    var app = builder.Build();
    app.UseCors("AllowAll");

    // すべてのリクエストを処理するエンドポイント
    app.Map("{*path}", async (HttpRequest request, HttpResponse response, string path, IHttpClientFactory httpClientFactory) =>
    {
        string cacheKey = $"{request.Method}:{path}{request.QueryString}";
        Console.WriteLine($"Request received: {cacheKey}");

        if (cache.TryGetValue(cacheKey, out CachedResponse? cachedResponse))
        {
            Console.WriteLine($"Cache HIT for: {cacheKey}");
            response.StatusCode = (int)cachedResponse.StatusCode;
            foreach (var header in cachedResponse.Headers)
            {
                response.Headers[header.Key] = new StringValues(header.Value);
            }
            response.Headers["X-Cache"] = "HIT";
            await response.Body.WriteAsync(cachedResponse.Body);
            return;
        }

        Console.WriteLine($"Cache MISS for: {cacheKey}");
        var client = httpClientFactory.CreateClient("OriginClient");
        var targetUri = new Uri(originUri, path + request.QueryString);
        using var forwardRequest = new HttpRequestMessage(new HttpMethod(request.Method), targetUri);

        if (request.ContentLength > 0 || request.Headers.TransferEncoding.Contains("chunked"))
        {
            forwardRequest.Content = new StreamContent(request.Body);
            if (request.Headers.ContentType.Count > 0)
                 forwardRequest.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(request.Headers.ContentType[0]!);
        }
        foreach (var header in request.Headers)
        {
             if (header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase) || header.Key.StartsWith("Content-", StringComparison.OrdinalIgnoreCase)) continue;
             forwardRequest.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
        }
         forwardRequest.Headers.TryAddWithoutValidation("X-Forwarded-For", request.HttpContext.Connection.RemoteIpAddress?.ToString());

        try
        {
            using var originResponse = await client.SendAsync(forwardRequest);

            response.StatusCode = (int)originResponse.StatusCode;

            var responseHeaders = new Dictionary<string, string[]>();
            // Response Headers と Content Headers を結合して処理
            foreach (var header in originResponse.Headers.Concat(originResponse.Content.Headers))
            {
                 responseHeaders[header.Key] = header.Value.ToArray();
                 response.Headers[header.Key] = header.Value.ToArray();
            }

            byte[] responseBodyBytes = await originResponse.Content.ReadAsByteArrayAsync();

            if (originResponse.IsSuccessStatusCode)
            {
                var newCacheEntry = new CachedResponse(originResponse.StatusCode, responseHeaders, responseBodyBytes);
                cache.AddOrUpdate(cacheKey, newCacheEntry, (key, oldEntry) => newCacheEntry);
                Console.WriteLine($"Cached response for: {cacheKey} (Size: {responseBodyBytes.Length} bytes)");
            }

            response.Headers["X-Cache"] = "MISS";
            Console.WriteLine($"Forwarded to {targetUri}, Status: {response.StatusCode}");
            await response.Body.WriteAsync(responseBodyBytes);

        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Error forwarding request to origin: {ex.Message}");
            response.StatusCode = StatusCodes.Status502BadGateway;
            await response.WriteAsync("Error connecting to the origin server.");
        }
         catch (Exception ex)
        {
             Console.WriteLine($"Unexpected error: {ex.Message}");
             response.StatusCode = StatusCodes.Status500InternalServerError;
             await response.WriteAsync("An internal server error occurred.");
        }
    });

    await app.RunAsync($"http://localhost:{port}");

}, portOption, originOption);

// キャッシュクリアコマンドの追加
var clearCacheCommand = new Command("--clear-cache", "Clears the in-memory cache (requires server restart to take effect).");
clearCacheCommand.SetHandler(() => {
    Console.WriteLine("Clearing the cache currently requires restarting the proxy server.");
    // cache.Clear();
});
rootCommand.AddCommand(clearCacheCommand);

// コマンドライン引数を解析して実行
await rootCommand.InvokeAsync(args);