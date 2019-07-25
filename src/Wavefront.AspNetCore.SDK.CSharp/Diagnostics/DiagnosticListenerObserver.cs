// From https://github.com/opentracing-contrib/csharp-netcore/blob/master/src/OpenTracing.Contrib.NetCore/Internal/DiagnosticListenerObserver.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OpenTracing;

namespace Wavefront.AspNetCore.SDK.CSharp.Diagnostics
{
    public abstract class DiagnosticListenerObserver
        : DiagnosticObserver, IObserver<KeyValuePair<string, object>>
    {
        protected DiagnosticListenerObserver(ILoggerFactory loggerFactory, ITracer tracer)
            : base(loggerFactory, tracer)
        {
        }

        /// <summary>
        /// The name of the <see cref="DiagnosticListener"/> that should be instrumented.
        /// </summary>
        public abstract string GetListenerName();

        public override IDisposable SubscribeIfMatch(DiagnosticListener diagnosticListener)
        {
            if (diagnosticListener.Name == GetListenerName())
            {
                return diagnosticListener.Subscribe(this);
            }

            return null;
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(KeyValuePair<string, object> value)
        {
            try
            {
                OnNext(value.Key, value.Value);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Event-Exception: {Event}", value.Key);
            }
        }

        public abstract void OnNext(string eventName, object arg);
    }
}
