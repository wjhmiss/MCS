using MCS.Grains.Interfaces;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace MCS.Grains
{
    public class APICallGrain : Grain, IAPICallGrain
    {
        private readonly ILogger<APICallGrain> _logger;
        private readonly IPersistentState<APICallState> _persistentState;
        private readonly HttpClient _httpClient;

        public APICallGrain(
            ILogger<APICallGrain> logger,
            [PersistentState("api-call", "Default")] IPersistentState<APICallState> persistentState,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _persistentState = persistentState;
            _httpClient = httpClientFactory.CreateClient();
        }

        public override async Task OnActivateAsync(CancellationToken cancellationToken)
        {
            await base.OnActivateAsync(cancellationToken);
            if (_persistentState.RecordExists)
            {
                _state = _persistentState.State;
            }
        }

        public async Task<string> CallExternalAPIAsync(APIRequest request)
        {
            _logger.LogInformation($"Calling external API: {request.Method} {request.Url}");

            try
            {
                var httpRequest = new HttpRequestMessage
                {
                    RequestUri = new Uri(request.Url),
                    Method = new HttpMethod(request.Method)
                };

                foreach (var header in request.Headers)
                {
                    httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                if (request.Body.Count > 0 &&
                    (request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase) ||
                     request.Method.Equals("PUT", StringComparison.OrdinalIgnoreCase) ||
                     request.Method.Equals("PATCH", StringComparison.OrdinalIgnoreCase)))
                {
                    var jsonBody = System.Text.Json.JsonSerializer.Serialize(request.Body);
                    httpRequest.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                }

                var response = await _httpClient.SendAsync(httpRequest);

                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"API call failed with status {response.StatusCode}: {responseContent}");
                }

                _logger.LogInformation($"API call successful: {request.Method} {request.Url}");
                return responseContent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to call external API: {request.Method} {request.Url}");
                throw;
            }
        }

        public async Task<string> CallExternalAPIWithRetryAsync(APIRequest request, int maxRetries = 3)
        {
            int retryCount = 0;
            Exception? lastException = null;

            while (retryCount < maxRetries)
            {
                try
                {
                    return await CallExternalAPIAsync(request);
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    retryCount++;
                    _logger.LogWarning($"API call failed (attempt {retryCount}/{maxRetries}): {ex.Message}");

                    if (retryCount < maxRetries)
                    {
                        var delay = TimeSpan.FromSeconds(Math.Pow(2, retryCount));
                        await Task.Delay(delay);
                    }
                }
            }

            throw new HttpRequestException($"API call failed after {maxRetries} retries", lastException);
        }

        private APICallState _state = new();
    }

    public class APICallState
    {
        public Dictionary<string, object> Config { get; set; } = new();
    }
}
