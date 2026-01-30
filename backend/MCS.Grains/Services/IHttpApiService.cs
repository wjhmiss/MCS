namespace MCS.Grains.Services;

/// <summary>
/// HTTP API 服务接口
/// 用于调用外部 HTTP API
/// </summary>
public interface IHttpApiService
{
    /// <summary>
    /// 发送 HTTP GET 请求
    /// </summary>
    /// <param name="url">请求 URL</param>
    /// <param name="headers">请求头</param>
    Task<HttpResponse> GetAsync(string url, Dictionary<string, string>? headers = null);

    /// <summary>
    /// 发送 HTTP POST 请求
    /// </summary>
    /// <param name="url">请求 URL</param>
    /// <param name="body">请求体</param>
    /// <param name="headers">请求头</param>
    Task<HttpResponse> PostAsync(string url, string? body = null, Dictionary<string, string>? headers = null);

    /// <summary>
    /// 发送 HTTP PUT 请求
    /// </summary>
    /// <param name="url">请求 URL</param>
    /// <param name="body">请求体</param>
    /// <param name="headers">请求头</param>
    Task<HttpResponse> PutAsync(string url, string? body = null, Dictionary<string, string>? headers = null);

    /// <summary>
    /// 发送 HTTP DELETE 请求
    /// </summary>
    /// <param name="url">请求 URL</param>
    /// <param name="headers">请求头</param>
    Task<HttpResponse> DeleteAsync(string url, Dictionary<string, string>? headers = null);

    /// <summary>
    /// 发送自定义 HTTP 请求
    /// </summary>
    /// <param name="url">请求 URL</param>
    /// <param name="method">HTTP 方法</param>
    /// <param name="body">请求体</param>
    /// <param name="headers">请求头</param>
    Task<HttpResponse> SendAsync(string url, string method, string? body = null, Dictionary<string, string>? headers = null);
}

/// <summary>
/// HTTP 响应类
/// </summary>
public class HttpResponse
{
    public int StatusCode { get; set; }
    public string? Content { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new();
    public bool IsSuccess => StatusCode >= 200 && StatusCode < 300;
}