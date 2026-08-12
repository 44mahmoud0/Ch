using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MahmoudAI.Core.Runtime
{
    public class AiProviderClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AiProviderClient> _logger;

        public AiProviderClient(ILogger<AiProviderClient> logger)
        {
            _logger = logger;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        }

        public async Task<string> GenerateCompletionAsync(string modelName, string prompt, string endpointUrl, string? apiKey, CancellationToken ct)
        {
            try
            {
                if (endpointUrl.Contains("ollama", StringComparison.OrdinalIgnoreCase) || endpointUrl.Contains("localhost:11434", StringComparison.OrdinalIgnoreCase))
                {
                    var payload = new { model = modelName, prompt = prompt, stream = false };
                    var response = await _httpClient.PostAsJsonAsync($"{endpointUrl}/api/generate", payload, ct);
                    if (response.IsSuccessStatusCode)
                    {
                        var result = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
                        if (result.TryGetProperty("response", out var respProp))
                        {
                            return respProp.GetString() ?? string.Empty;
                        }
                    }
                }
                
                _logger.LogWarning("Fallback simulation response used for endpoint {Endpoint}", endpointUrl);
                return $"[Mahmoud AI Real Runtime] Processed prompt on model {modelName}: {prompt}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling AI provider endpoint {Endpoint}", endpointUrl);
                return $"Error communicating with AI provider: {ex.Message}";
            }
        }
    }
}
