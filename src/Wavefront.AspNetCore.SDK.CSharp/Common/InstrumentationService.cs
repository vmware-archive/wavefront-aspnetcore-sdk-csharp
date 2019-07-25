// From https://github.com/opentracing-contrib/csharp-netcore/blob/master/src/OpenTracing.Contrib.NetCore/InstrumentationService.cs

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Wavefront.AspNetCore.SDK.CSharp.Diagnostics;

namespace Wavefront.AspNetCore.SDK.CSharp.Common
{
    /// <summary>
    /// Starts and stops all Wavefront instrumentation components.
    /// </summary>
    public class InstrumentationService : IHostedService, IDisposable
    {
        private readonly DiagnosticManager _diagnosticManager;

        public InstrumentationService(DiagnosticManager diagnosticManager)
        {
            _diagnosticManager = diagnosticManager ??
                throw new ArgumentNullException(nameof(diagnosticManager));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _diagnosticManager.Start();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _diagnosticManager.Stop();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _diagnosticManager.Dispose();
        }
    }
}
