using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wavefront.SDK.CSharp.Common.Application;

namespace Wavefront.AspNetCore.SDK.CSharp.Common
{
    /// <summary>
    ///     Hosted service (activated at application startup) that runs a
    ///     <see cref="HeartbeaterService"/> as a background task.
    /// </summary>
    public class HeartbeaterHostedService : IHostedService, IDisposable
    {
        private readonly HeartbeaterService _heartbeaterService;

        public HeartbeaterHostedService(WavefrontAspNetCoreReporter wfAspNetCoreReporter,
            ILoggerFactory loggerFactory)
        {
            _heartbeaterService = new HeartbeaterService(
                wfAspNetCoreReporter.WavefrontSender,
                wfAspNetCoreReporter.ApplicationTags,
                new List<string> { Constants.AspNetCoreComponent },
                wfAspNetCoreReporter.Source,
                loggerFactory
            );
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _heartbeaterService.Start();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _heartbeaterService.Stop();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _heartbeaterService.Dispose();
        }
    }
}
