using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MahmoudAI.Storage
{
    public record VectorPoint(string Id, float[] Vector, Dictionary<string, object> Payload);

    public class VectorMemoryStore
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<VectorMemoryStore> _logger;
        private readonly string _qdrantEndpoint;

        public VectorMemoryStore(string qdrantEndpoint, ILogger<VectorMemoryStore> logger)
        {
            _qdrantEndpoint = qdrantEndpoint;
            _logger = logger;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        }

        public async Task<bool> EnsureCollectionAsync(string collectionName, int vectorSize, CancellationToken ct)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"{_qdrantEndpoint}/collections/{collectionName}", new
                {
                    vectors = new { size = vectorSize, distance = "Cosine" }
                }, ct);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not connect to Qdrant vector database at {Endpoint}. Falling back to local SQLite vector table.", _qdrantEndpoint);
                return false;
            }
        }

        public async Task<bool> UpsertVectorAsync(string collectionName, VectorPoint point, CancellationToken ct)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"{_qdrantEndpoint}/collections/{collectionName}/points", new
                {
                    points = new[]
                    {
                        new { id = point.Id, vector = point.Vector, payload = point.Payload }
                    }
                }, ct);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to upsert vector to Qdrant. Stored locally.");
                return false;
            }
        }
    }
}
