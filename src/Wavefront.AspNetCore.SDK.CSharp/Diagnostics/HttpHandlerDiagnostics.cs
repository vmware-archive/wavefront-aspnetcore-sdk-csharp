// From https://github.com/opentracing-contrib/csharp-netcore/blob/master/src/OpenTracing.Contrib.NetCore/CoreFx/HttpHandlerDiagnostics.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenTracing;
using OpenTracing.Noop;
using OpenTracing.Propagation;
using OpenTracing.Tag;
using Wavefront.AspNetCore.SDK.CSharp.OpenTracing;
using static Wavefront.AspNetCore.SDK.CSharp.Common.Constants;
using static Wavefront.SDK.CSharp.Common.Constants;

namespace Wavefront.AspNetCore.SDK.CSharp.Diagnostics
{
    /// <summary>
    /// Instruments outgoing HTTP calls that use <see cref="HttpClientHandler"/>.
    /// </summary>
    public class HttpHandlerDiagnostics : DiagnosticListenerObserver
    {
        public const string DiagnosticListenerName = "HttpHandlerDiagnosticListener";

        private const string PropertiesKey = "ot-Span";

        private static readonly PropertyFetcher _activityStart_RequestFetcher =
            new PropertyFetcher("Request");
        private static readonly PropertyFetcher _activityStop_RequestFetcher =
            new PropertyFetcher("Request");
        private static readonly PropertyFetcher _activityStop_ResponseFetcher =
            new PropertyFetcher("Response");
        private static readonly PropertyFetcher _activityStop_RequestTaskStatusFetcher =
            new PropertyFetcher("RequestTaskStatus");
        private static readonly PropertyFetcher _exception_RequestFetcher =
            new PropertyFetcher("Request");
        private static readonly PropertyFetcher _exception_ExceptionFetcher =
            new PropertyFetcher("Exception");

        public override string GetListenerName() => DiagnosticListenerName;

        public HttpHandlerDiagnostics(ILoggerFactory loggerFactory, ITracer tracer)
            : base(loggerFactory, tracer)
        {
        }

        public override void OnNext(string eventName, object arg)
        {
            switch (eventName)
            {
                case "System.Net.Http.HttpRequestOut.Start":
                    OnHttpRequestOutStart(arg);
                    break;

                case "System.Net.Http.Exception":
                    OnException(arg);
                    break;

                case "System.Net.Http.HttpRequestOut.Stop":
                    OnHttpRequestOutStop(arg);
                    break;
            }
        }

        private void OnHttpRequestOutStart(object arg)
        {
            if (Tracer is NoopTracer)
            {
                return;
            }

            var request = (HttpRequestMessage)_activityStart_RequestFetcher.Fetch(arg);

            if (IgnoreRequest(request))
            {
                return;
            }

            ISpan span = Tracer.BuildSpan(request.Method.Method)
                .WithTag(Tags.SpanKind, Tags.SpanKindClient)
                .WithTag(Tags.Component, HttpHandlerComponent)
                .WithTag(Tags.HttpMethod, request.Method.ToString())
                .WithTag(Tags.HttpUrl, request.RequestUri.ToString())
                .Start();

            Tracer.Inject(span.Context, BuiltinFormats.HttpHeaders,
                          new HttpHeadersInjectAdapter(request.Headers));

            request.Properties.Add(PropertiesKey, span);
        }

        private void OnException(object arg)
        {
            var request = (HttpRequestMessage)_exception_RequestFetcher.Fetch(arg);

            if (request.Properties.TryGetValue(PropertiesKey, out object objSpan) && objSpan is ISpan span)
            {
                var exception = (Exception)_exception_ExceptionFetcher.Fetch(arg);

                span.SetException(exception);
            }
        }

        private void OnHttpRequestOutStop(object arg)
        {
            var request = (HttpRequestMessage)_activityStop_RequestFetcher.Fetch(arg);

            if (request.Properties.TryGetValue(PropertiesKey, out object objSpan) && objSpan is ISpan span)
            {
                var response = (HttpResponseMessage)_activityStop_ResponseFetcher.Fetch(arg);
                var requestTaskStatus = (TaskStatus)_activityStop_RequestTaskStatusFetcher.Fetch(arg);

                if (response != null)
                {
                    span.SetTag(Tags.HttpStatus, (int)response.StatusCode);
                }

                if (requestTaskStatus == TaskStatus.Canceled || requestTaskStatus == TaskStatus.Faulted ||
                    (response != null && 400 <= (int)response.StatusCode && (int)response.StatusCode <= 599))
                {
                    span.SetTag(Tags.Error, true);
                }

                if (response.Headers.TryGetValues(WavefrontSpanHeader, out IEnumerable<string> headerValues))
                {
                    span.SetOperationName($"{request.Method.Method}-{headerValues.First()}");
                }

                span.Finish();
            }
        }

        private bool IgnoreRequest(HttpRequestMessage request)
        {
            return request.Headers.Contains(WavefrontIgnoreHeader);
        }
    }
}
