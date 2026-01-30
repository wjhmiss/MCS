using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;

namespace MCS.Grains.Services;

/// <summary>
/// HTTP API 服务实现类
/// 使用 HttpClient 实现 HTTP 请求功能
/// </summary>
public class HttpApiService : IHttpApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpApiService> _logger;

    public HttpApiService(IHttpClientFactory httpClientFactory, ILogger<HttpApiService> logger)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("MCS.Orleans.HttpClient");
        _httpClient.Timeout = TimeSpan.FromMinutes(5);
    }

    public async Task<HttpResponse> GetAsync(string url, Dictionary<string, string>? headers = null)
    {
        return await SendAsync(url, "GET", null, headers);
    }

    public async Task<HttpResponse> PostAsync(string url, string? body = null, Dictionary<string, string>? headers = null)
    {
        return await SendAsync(url, "POST", body, headers);
    }

    public async Task<HttpResponse> PutAsync(string url, string? body = null, Dictionary<string, string>? headers = null)
    {
        return await SendAsync(url, "PUT", body, headers);
    }

    public async Task<HttpResponse> DeleteAsync(string url, Dictionary<string, string>? headers = null)
    {
        return await SendAsync(url, "DELETE", null, headers);
    }

    public async Task<HttpResponse> SendAsync(string url, string method, string? body = null, Dictionary<string, string>? headers = null)
    {
        try
        {
            _logger.LogInformation("Sending HTTP {Method} request to {Url}", method, url);

            var request = new HttpRequestMessage
            {
                RequestUri = new Uri(url),
                Method = new HttpMethod(method.ToUpper())
            };

            if (!string.IsNullOrEmpty(body))
            {
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }

            if (headers != null)
            {
                foreach (var header in headers)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            var result = new HttpResponse
            {
                StatusCode = (int)response.StatusCode,
                Content = content,
                Headers = response.Headers.ToDictionary(h => h.Key, h => h.Value.First())
            };

            _logger.LogInformation("HTTP {Method} request to {Url} completed with status {StatusCode}", method, url, result.StatusCode);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HTTP {Method} request to {Url} failed", method, url);
            throw;
        }
    }
}