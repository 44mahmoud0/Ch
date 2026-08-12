using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MahmoudAI.Core.Runtime
{
    public record BenchmarkResult(string ModelName, double LatencyMs, int TokensPerSecond, bool Success);

    public class ModelBenchmarkCenter
    {
        private readonly AiProviderClient _providerClient;
        private readonly ILogger<ModelBenchmarkCenter> _logger;

        public ModelBenchmarkCenter(AiProviderClient providerClient, ILogger<ModelBenchmarkCenter> logger)
        {
            _providerClient = providerClient;
            _logger = logger;
        }

        public async Task<BenchmarkResult> BenchmarkModelAsync(string modelName, string endpointUrl, CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                string prompt = "Benchmark test prompt: compute prime numbers.";
                string response = await _providerClient.GenerateCompletionAsync(modelName, prompt, endpointUrl, null, ct);
                sw.Stop();

                double latency = sw.ElapsedMilliseconds;
                int tps = (int)(response.Length / Math.Max(0.1, sw.Elapsed.TotalSeconds));
                
                _logger.LogInformation("Model {Model} benchmarked: {Latency}ms, {Tps} chars/sec", modelName, latency, tps);
                return new BenchmarkResult(modelName, latency, tps, true);
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "Benchmark failed for model {Model}", modelName);
                return new BenchmarkResult(modelName, sw.ElapsedMilliseconds, 0, false);
            }
        }
    }
}
