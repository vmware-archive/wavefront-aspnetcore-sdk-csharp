using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wavefront.CSharp.SDK.Entities.Metrics;

namespace Wavefront.AspNetCore.SDK.CSharp
{
    public class HeartbeaterService : IHostedService, IDisposable
    {
        private readonly ILogger<HeartbeaterService> logger;
        private readonly IWavefrontMetricSender wavefrontMetricSender;
        private readonly string source;
        private readonly IDictionary<string, string> heartbeatMetricTags;
        private Timer timer;

        public HeartbeaterService(
            ILogger<HeartbeaterService> logger,
            IWavefrontMetricSender wavefrontMetricSender,
            ApplicationTags applicationTags,
            IOptions<WavefrontReportingOptions> wavefrontReportingOptions)
        {
            this.logger = logger;
            this.wavefrontMetricSender = wavefrontMetricSender;
            source = wavefrontReportingOptions.Value.Source;
            heartbeatMetricTags = new Dictionary<string, string>
            {
                { Constants.ApplicationTagKey, applicationTags.Application },
                { Constants.ClusterTagKey, applicationTags.Cluster ?? Constants.NullTagValue },
                { Constants.ServiceTagKey, applicationTags.Service },
                { Constants.ShardTagKey, applicationTags.Shard ?? Constants.NullTagValue },
                { Constants.ComponentTagKey, wavefrontReportingOptions.Value.Component }
            };
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            timer = new Timer(SendHeartbeat, null, TimeSpan.FromMinutes(1),
                TimeSpan.FromMinutes(5));
            return Task.CompletedTask;
        }

        private void SendHeartbeat(object state)
        {
            try
            {
                wavefrontMetricSender.SendMetric(Constants.HeartbeatMetric, 1.0,
                                                 DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                                                 source, heartbeatMetricTags);
            }
            catch (Exception)
            {
                logger.LogWarning($"Cannot report {Constants.HeartbeatMetric} to Wavefront");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            timer?.Dispose();
        }
    }
}
