using System.Net; // HttpStatusCode のため
using System.Collections.Generic; // Dictionary のため
using System; // 

public class CachedResponse
{
    public HttpStatusCode StatusCode { get; }
    public Dictionary<string, string[]> Headers { get; }
    public byte[] Body { get; }
    public DateTime CachedAt { get; }

    public CachedResponse(HttpStatusCode statusCode, Dictionary<string, string[]> headers, byte[] body)
    {
        StatusCode = statusCode;
        Headers = headers;
        Body = body;
        CachedAt = DateTime.UtcNow;
    }
}