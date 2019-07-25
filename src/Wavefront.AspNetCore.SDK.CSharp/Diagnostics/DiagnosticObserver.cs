// From https://github.com/opentracing-contrib/csharp-netcore/blob/master/src/OpenTracing.Contrib.NetCore/Internal/DiagnosticObserver.cs

using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OpenTracing;

namespace Wavefront.AspNetCore.SDK.CSharp.Diagnostics
{
    public abstract class DiagnosticObserver
    {
        public ILogger Logger { get; }

        public ITracer Tracer { get; }

        protected DiagnosticObserver(ILoggerFactory loggerFactory, ITracer tracer)
        {
            if (loggerFactory == null)
                throw new ArgumentNullException(nameof(loggerFactory));

            Tracer = tracer ?? throw new ArgumentNullException(nameof(tracer));
            Logger = loggerFactory.CreateLogger(GetType());
        }

        public abstract IDisposable SubscribeIfMatch(DiagnosticListener diagnosticListener);
    }
}
