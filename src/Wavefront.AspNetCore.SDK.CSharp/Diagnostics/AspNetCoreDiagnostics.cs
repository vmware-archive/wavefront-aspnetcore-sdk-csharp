// From https://github.com/opentracing-contrib/csharp-netcore/blob/master/src/OpenTracing.Contrib.NetCore/AspNetCore/AspNetCoreDiagnostics.cs

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using App.Metrics;
using App.Metrics.Counter;
using App.Metrics.Gauge;
using App.Metrics.Histogram;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Logging;
using OpenTracing;
using OpenTracing.Noop;
using OpenTracing.Propagation;
using OpenTracing.Tag;
using Wavefront.AspNetCore.SDK.CSharp.Common;
using Wavefront.AspNetCore.SDK.CSharp.OpenTracing;
using Wavefront.SDK.CSharp.Common.Application;
using static Wavefront.AspNetCore.SDK.CSharp.Common.Constants;
using static Wavefront.SDK.CSharp.Common.Constants;

namespace Wavefront.AspNetCore.SDK.CSharp.Diagnostics
{
    /// <summary>
    /// Instruments ASP.NET Core.
    /// </summary>
    public class AspNetCoreDiagnostics : DiagnosticListenerObserver
    {
        public const string DiagnosticListenerName = "Microsoft.AspNetCore";

        private static readonly PropertyFetcher _httpRequestIn_start_HttpContextFetcher =
            new PropertyFetcher("HttpContext");
        private static readonly PropertyFetcher _httpRequestIn_stop_HttpContextFetcher =
            new PropertyFetcher("HttpContext");
        private static readonly PropertyFetcher _unhandledException_ExceptionFetcher =
            new PropertyFetcher("exception");
        private static readonly PropertyFetcher _beforeAction_httpContextFetcher =
            new PropertyFetcher("httpContext");
        private static readonly PropertyFetcher _beforeAction_ActionDescriptorFetcher =
            new PropertyFetcher("actionDescriptor");
        private static readonly PropertyFetcher _afterAction_httpContextFetcher =
            new PropertyFetcher("httpContext");
        private static readonly PropertyFetcher _afterAction_ActionDescriptorFetcher =
            new PropertyFetcher("actionDescriptor");

        private static readonly string StartTimeMillisKey = "Wavefront.StartTimeMillis";
        private static readonly string ControllerNameKey = "Wavefront.ControllerName";
        private static readonly string ActionNameKey = "Wavefront.ActionName";
        private static readonly string ResponseMetricPrefixKey = "Wavefront.ResponseMetricPrefix";

        private readonly IMetrics _metrics;
        private readonly ApplicationTags _applicationTags;

        private readonly MetricTags _overallAggregatedPerSourceTags;
        private readonly MetricTags _overallAggregatedPerShardTags;
        private readonly MetricTags _overallAggregatedPerServiceTags;
        private readonly MetricTags _overallAggregatedPerClusterTags;
        private readonly MetricTags _overallAggregatedPerApplicationTags;

        private readonly WavefrontGaugeOptions _totalInflightRequestGauge;

        private readonly ConcurrentDictionary<WavefrontGaugeOptions, StrongBox<int>> _gauges =
            new ConcurrentDictionary<WavefrontGaugeOptions, StrongBox<int>>();

        public override string GetListenerName() => DiagnosticListenerName;

        public AspNetCoreDiagnostics(ILoggerFactory loggerFactory, ITracer tracer,
            WavefrontAspNetCoreReporter wfAspNetCoreReporter) : base(loggerFactory, tracer)
        {
            _metrics = wfAspNetCoreReporter.Metrics;
            _applicationTags = wfAspNetCoreReporter.ApplicationTags;

            _overallAggregatedPerSourceTags = GetTags(true, true, true, null, null, null);
            _overallAggregatedPerShardTags =
                GetTags(true, true, true, null, null, WavefrontProvidedSource);
            _overallAggregatedPerServiceTags =
                GetTags(true, true, false, null, null, WavefrontProvidedSource);
            _overallAggregatedPerClusterTags =
                GetTags(true, false, false, null, null, WavefrontProvidedSource);
            _overallAggregatedPerApplicationTags =
                GetTags(false, false, false, null, null, WavefrontProvidedSource);

            _totalInflightRequestGauge = new WavefrontGaugeOptions
            {
                Context = AspNetCoreContext,
                Name = "total_requests.inflight",
                Tags = _overallAggregatedPerSourceTags,
                MeasurementUnit = Unit.Requests
            };
        }

        public override void OnNext(string eventName, object arg)
        {
            switch (eventName)
            {
                case "Microsoft.AspNetCore.Hosting.HttpRequestIn.Start":
                    OnHttpRequestInStart(arg);
                    break;

                case "Microsoft.AspNetCore.Mvc.BeforeAction":
                    OnBeforeAction(arg);
                    break;

                case "Microsoft.AspNetCore.Mvc.AfterAction":
                    OnAfterAction(arg);
                    break;

                case "Microsoft.AspNetCore.Diagnostics.UnhandledException":
                    OnUnhandledException(arg);
                    break;

                case "Microsoft.AspNetCore.Hosting.HttpRequestIn.Stop":
                    OnHttpRequestInStop(arg);
                    break;
            }
        }

        private void OnHttpRequestInStart(object arg)
        {
            var httpContext = (HttpContext)_httpRequestIn_start_HttpContextFetcher.Fetch(arg);
            if (httpContext == null)
            {
                Logger.LogInformation("No metrics/spans will be recorded because HttpContext payload is null");
                return;
            }
            var request = httpContext.Request;

            httpContext.Items[StartTimeMillisKey] = GetCurrentMillis();

            if (Tracer is NoopTracer)
            {
                return;
            }

            var extractedSpanContext = Tracer.Extract(BuiltinFormats.HttpHeaders,
                new RequestHeadersExtractAdapter(request.Headers));

            Tracer.BuildSpan(httpContext.Request.Method)
                .AsChildOf(extractedSpanContext)
                .WithTag(Tags.SpanKind, Tags.SpanKindServer)
                .WithTag(Tags.Component, AspNetCoreComponent)
                .WithTag(Tags.HttpMethod, request.Method)
                .WithTag(Tags.HttpUrl, GetDisplayUrl(request))
                .StartActive();
        }

        private void OnBeforeAction(object arg)
        {
            // NOTE: This event is the start of the action pipeline. The action has been selected, the route
            //       has been selected but no filters have run and model binding hasn't occured.

            var httpContext = (HttpContext)_beforeAction_httpContextFetcher.Fetch(arg);
            var actionDescriptor = (ActionDescriptor)_beforeAction_ActionDescriptorFetcher.Fetch(arg);
            if (httpContext == null || actionDescriptor == null)
            {
                return;
            }
            var request = httpContext.Request;
            string routeTemplate = actionDescriptor.AttributeRouteInfo.Template;
            var controllerActionDescriptor = actionDescriptor as ControllerActionDescriptor;
            string controllerName = controllerActionDescriptor?.ControllerTypeInfo.FullName;
            string actionName = controllerActionDescriptor?.ActionName;

            httpContext.Response.Headers.Add(WavefrontSpanHeader, routeTemplate);

            // Update the active tracing span with MVC info
            ISpan span = Tracer.ActiveSpan;
            if (span != null)
            {
                if (controllerActionDescriptor == null)
                {
                    span.SetOperationName(actionDescriptor.DisplayName);
                }
                else
                {
                    span.SetOperationName($"{controllerActionDescriptor.ControllerTypeInfo.Name}.{actionName}");
                    span.SetTag(ControllerTagKey, controllerName);
                }
                span.SetTag(PathTagKey, routeTemplate);
            }

            Interlocked.Increment(ref GetGaugeValue(_totalInflightRequestGauge).Value);

            if (controllerActionDescriptor == null)
            {
                return;
            }

            string requestMetricPrefix = MetricNameUtils.MetricName(request, routeTemplate);
            if (requestMetricPrefix != null)
            {
                var completeTags = GetTags(true, true, true, controllerName, actionName, null);

                /* Gauges
                 * 1) AspNetCore.request.api.v2.alert.summary.GET.inflight.value
                 * 2) AspNetCore.total_requests.inflight.value
                 */
                var inflightRequestGauge = new WavefrontGaugeOptions
                {
                    Context = AspNetCoreContext,
                    Name = requestMetricPrefix + ".inflight",
                    Tags = completeTags,
                    MeasurementUnit = Unit.Requests
                };
                Interlocked.Increment(ref GetGaugeValue(inflightRequestGauge).Value);
            }
        }

        private void OnAfterAction(object arg)
        {
            var httpContext = (HttpContext)_afterAction_httpContextFetcher.Fetch(arg);
            var actionDescriptor = (ActionDescriptor)_afterAction_ActionDescriptorFetcher.Fetch(arg);
            if (httpContext == null || actionDescriptor == null)
            {
                return;
            }
            var request = httpContext.Request;
            var response = httpContext.Response;
            string routeTemplate = actionDescriptor.AttributeRouteInfo.Template;
            var controllerActionDescriptor = actionDescriptor as ControllerActionDescriptor;
            string controllerName = controllerActionDescriptor?.ControllerTypeInfo.FullName;
            string actionName = controllerActionDescriptor?.ActionName;

            Interlocked.Decrement(ref _gauges[_totalInflightRequestGauge].Value);

            if (controllerActionDescriptor == null)
            {
                return;
            }

            string requestMetricPrefix = MetricNameUtils.MetricName(request, routeTemplate);
            if (requestMetricPrefix != null)
            {
                var completeTags = GetTags(true, true, true, controllerName, actionName, null);

                /* 
                 * Gauges
                 * 1) AspNetCore.request.api.v2.alert.summary.GET.inflight.value
                 * 2) AspNetCore.total_requests.inflight.value
                 */
                var inflightRequestGauge = new WavefrontGaugeOptions
                {
                    Context = AspNetCoreContext,
                    Name = requestMetricPrefix + ".inflight",
                    Tags = completeTags,
                    MeasurementUnit = Unit.Requests
                };
                Interlocked.Decrement(ref _gauges[inflightRequestGauge].Value);
            }

            string responseMetricPrefix = MetricNameUtils.MetricName(request, routeTemplate, response);
            if (responseMetricPrefix != null)
            {
                httpContext.Items[ControllerNameKey] = controllerName;
                httpContext.Items[ActionNameKey] = actionName;
                httpContext.Items[ResponseMetricPrefixKey] = responseMetricPrefix;
            }
        }

        private void OnUnhandledException(object arg)
        {
            ISpan span = Tracer.ActiveSpan;
            if (span != null)
            {
                var exception = (Exception)_unhandledException_ExceptionFetcher.Fetch(arg);
                span.SetException(exception);
            }
        }

        private void OnHttpRequestInStop(object arg)
        {
            var httpContext = (HttpContext)_httpRequestIn_stop_HttpContextFetcher.Fetch(arg);
            if (httpContext == null)
            {
                return;
            }
            var statusCode = httpContext.Response.StatusCode;

            IScope scope = Tracer.ScopeManager.Active;
            if (scope != null)
            {
                scope.Span.SetTag(Tags.HttpStatus, statusCode);
                if (IsErrorStatusCode(statusCode))
                {
                    scope.Span.SetTag(Tags.Error, true);
                }
                scope.Dispose();
            }

            if (!httpContext.Items.TryGetValue(ControllerNameKey, out var controllerNameObj) ||
                !httpContext.Items.TryGetValue(ActionNameKey, out var actionNameObj) ||
                !httpContext.Items.TryGetValue(ResponseMetricPrefixKey, out var responseMetricPrefixObj))
            {
                return;
            }

            string controllerName = (string)controllerNameObj;
            string actionName = (string)actionNameObj;
            string responseMetricPrefix = (string)responseMetricPrefixObj;
            string responseMetricPrefixWithStatus = $"{responseMetricPrefix}.{statusCode}";

            var completeTags = GetTags(true, true, true, controllerName, actionName, null);
            var aggregatedPerShardTags =
                GetTags(true, true, true, controllerName, actionName, WavefrontProvidedSource);
            var aggregatedPerServiceTags =
                GetTags(true, true, false, controllerName, actionName, WavefrontProvidedSource);
            var aggregatedPerClusterTags =
                GetTags(true, false, false, controllerName, actionName, WavefrontProvidedSource);
            var aggregatedPerApplicationTags =
                GetTags(false, false, false, controllerName, actionName, WavefrontProvidedSource);

            /*
             * Granular response metrics
             * 1) AspNetCore.response.api.v2.alert.summary.GET.200.cumulative.count (Counter)
             * 2) AspNetCore.response.api.v2.alert.summary.GET.200.aggregated_per_shard.count (DeltaCounter)
             * 3) AspNetCore.response.api.v2.alert.summary.GET.200.aggregated_per_service.count (DeltaCounter)
             * 4) AspNetCore.response.api.v2.alert.summary.GET.200.aggregated_per_cluster.count (DeltaCounter)
             * 5) AspNetCore.response.api.v2.alert.summary.GET.200.aggregated_per_application.count (DeltaCounter)
             * 6) AspNetCore.response.api.v2.alert.summary.GET.errors.count (Counter)
             */
            _metrics.Measure.Counter.Increment(new CounterOptions
            {
                Context = AspNetCoreContext,
                Name = responseMetricPrefixWithStatus + ".cumulative",
                Tags = completeTags,
                MeasurementUnit = ResponseUnit
            });
            if (_applicationTags.Shard != null)
            {
                _metrics.Measure.Counter.Increment(
                    new DeltaCounterOptions.Builder(responseMetricPrefixWithStatus + ".aggregated_per_shard")
                                           .Context(AspNetCoreContext)
                                           .Tags(aggregatedPerShardTags)
                                           .MeasurementUnit(ResponseUnit)
                                           .Build());
            }
            _metrics.Measure.Counter.Increment(
                new DeltaCounterOptions.Builder(responseMetricPrefixWithStatus + ".aggregated_per_service")
                                       .Context(AspNetCoreContext)
                                       .Tags(aggregatedPerServiceTags)
                                       .MeasurementUnit(ResponseUnit)
                                       .Build());
            if (_applicationTags.Cluster != null)
            {
                _metrics.Measure.Counter.Increment(
                    new DeltaCounterOptions.Builder(responseMetricPrefixWithStatus + ".aggregated_per_cluster")
                                           .Context(AspNetCoreContext)
                                           .Tags(aggregatedPerClusterTags)
                                           .MeasurementUnit(ResponseUnit)
                                           .Build());
            }
            _metrics.Measure.Counter.Increment(
                new DeltaCounterOptions.Builder(responseMetricPrefixWithStatus + ".aggregated_per_application")
                                       .Context(AspNetCoreContext)
                                       .Tags(aggregatedPerApplicationTags)
                                       .MeasurementUnit(ResponseUnit)
                                       .Build());


            if (IsErrorStatusCode(statusCode))
            {
                /*
                 * Overall error response metrics
                 * 1) AspNetCore.response.errors.aggregated_per_source.count (Counter)
                 * 2) AspNetCore.response.errors.aggregated_per_shard.count (DeltaCounter)
                 * 3) AspNetCore.response.errors.aggregated_per_service.count (DeltaCounter)
                 * 4) AspNetCore.response.errors.aggregated_per_cluster.count (DeltaCounter)
                 * 5) AspNetCore.response.errors.aggregated_per_application.count (DeltaCounter)
                 */
                _metrics.Measure.Counter.Increment(new CounterOptions
                {
                    Context = AspNetCoreContext,
                    Name = responseMetricPrefix + ".errors",
                    Tags = completeTags,
                    MeasurementUnit = Unit.Errors
                });
                _metrics.Measure.Counter.Increment(new CounterOptions
                {
                    Context = AspNetCoreContext,
                    Name = "response.errors",
                    Tags = completeTags,
                    MeasurementUnit = Unit.Errors
                });
                _metrics.Measure.Counter.Increment(new CounterOptions
                {
                    Context = AspNetCoreContext,
                    Name = "response.errors.aggregated_per_source",
                    Tags = _overallAggregatedPerSourceTags,
                    MeasurementUnit = Unit.Errors
                });
                if (_applicationTags.Shard != null)
                {
                    _metrics.Measure.Counter.Increment(
                        new DeltaCounterOptions.Builder("response.errors.aggregated_per_shard")
                                                .Context(AspNetCoreContext)
                                                .Tags(_overallAggregatedPerShardTags)
                                                .MeasurementUnit(Unit.Errors)
                                                .Build());
                }
                _metrics.Measure.Counter.Increment(
                    new DeltaCounterOptions.Builder("response.errors.aggregated_per_service")
                                            .Context(AspNetCoreContext)
                                            .Tags(_overallAggregatedPerServiceTags)
                                            .MeasurementUnit(Unit.Errors)
                                            .Build());
                if (_applicationTags.Cluster != null)
                {
                    _metrics.Measure.Counter.Increment(
                        new DeltaCounterOptions.Builder("response.errors.aggregated_per_cluster")
                                                .Context(AspNetCoreContext)
                                                .Tags(_overallAggregatedPerClusterTags)
                                                .MeasurementUnit(Unit.Errors)
                                                .Build());
                }
                _metrics.Measure.Counter.Increment(
                    new DeltaCounterOptions.Builder("response.errors.aggregated_per_application")
                                            .Context(AspNetCoreContext)
                                            .Tags(_overallAggregatedPerApplicationTags)
                                            .MeasurementUnit(Unit.Errors)
                                            .Build());
            }

            /*
             * Overall response metrics
             * 1) AspNetCore.response.completed.aggregated_per_source.count (Counter)
             * 2) AspNetCore.response.completed.aggregated_per_shard.count (DeltaCounter)
             * 3) AspNetCore.response.completed.aggregated_per_service.count (DeltaCounter)
             * 3) AspNetCore.response.completed.aggregated_per_cluster.count (DeltaCounter)
             * 5) AspNetCore.response.completed.aggregated_per_application.count (DeltaCounter)
             */
            _metrics.Measure.Counter.Increment(new CounterOptions
            {
                Context = AspNetCoreContext,
                Name = "response.completed.aggregated_per_source",
                Tags = _overallAggregatedPerSourceTags,
                MeasurementUnit = ResponseUnit
            });
            if (_applicationTags.Shard != null)
            {
                _metrics.Measure.Counter.Increment(
                    new DeltaCounterOptions.Builder("response.completed.aggregated_per_shard")
                                           .Context(AspNetCoreContext)
                                           .Tags(_overallAggregatedPerShardTags)
                                           .MeasurementUnit(ResponseUnit)
                                           .Build());
            }
            _metrics.Measure.Counter.Increment(
                new DeltaCounterOptions.Builder("response.completed.aggregated_per_service")
                                       .Context(AspNetCoreContext)
                                       .Tags(_overallAggregatedPerServiceTags)
                                       .MeasurementUnit(ResponseUnit)
                                       .Build());
            if (_applicationTags.Cluster != null)
            {
                _metrics.Measure.Counter.Increment(
                    new DeltaCounterOptions.Builder("response.completed.aggregated_per_cluster")
                                           .Context(AspNetCoreContext)
                                           .Tags(_overallAggregatedPerClusterTags)
                                           .MeasurementUnit(ResponseUnit)
                                           .Build());
            }
            _metrics.Measure.Counter.Increment(
                new DeltaCounterOptions.Builder("response.completed.aggregated_per_application")
                                       .Context(AspNetCoreContext)
                                       .Tags(_overallAggregatedPerApplicationTags)
                                       .MeasurementUnit(ResponseUnit)
                                       .Build());

            if (httpContext.Items.TryGetValue(StartTimeMillisKey, out var startTimeMillis))
            {
                /*
                 * WavefrontHistograms
                 * 1) AspNetCore.response.api.v2.alert.summary.GET.200.latency
                 */
                long apiLatency = GetCurrentMillis() - (long)startTimeMillis;
                _metrics.Measure.Histogram.Update(
                    new WavefrontHistogramOptions.Builder(responseMetricPrefixWithStatus + ".latency")
                                                 .Context(AspNetCoreContext)
                                                 .Tags(completeTags)
                                                 .MeasurementUnit(MillisecondUnit)
                                                 .Build(), apiLatency);

                /*
                 * Total time spent counter
                 *    AspNetCore.response.api.v2.alert.summary.GET.200.total_time
                 */
                _metrics.Measure.Counter.Increment(new CounterOptions
                {
                    Context = AspNetCoreContext,
                    Name = responseMetricPrefixWithStatus + ".total_time",
                    Tags = completeTags,
                    MeasurementUnit = MillisecondUnit
                }, apiLatency);
            }
        }

        private static string GetDisplayUrl(HttpRequest request)
        {
            if (request.Host.HasValue)
            {
                return request.GetDisplayUrl();
            }
            return $"{request.Scheme}://{request.PathBase.Value}{request.Path.Value}{request.QueryString.Value}";
        }

        private static long GetCurrentMillis()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        private StrongBox<int> GetGaugeValue(WavefrontGaugeOptions gaugeOptions)
        {
            return _gauges.GetOrAdd(gaugeOptions, key =>
            {
                StrongBox<int> toReturn = new StrongBox<int>();
                _metrics.Measure.Gauge.SetValue(gaugeOptions, () => toReturn.Value);
                return toReturn;
            });
        }

        private MetricTags GetTags(
            bool includeCluster, bool includeService, bool includeShard,
            string controllerName, string actionName, string source)
        {
            var tagsDictionary = new Dictionary<string, string>();

            if (includeCluster)
            {
                tagsDictionary.Add(ClusterTagKey, _applicationTags.Cluster ?? NullTagValue);
            }
            if (includeService)
            {
                tagsDictionary.Add(ServiceTagKey, _applicationTags.Service);
            }
            if (includeShard)
            {
                tagsDictionary.Add(ShardTagKey, _applicationTags.Shard ?? NullTagValue);
            }
            if (controllerName != null)
            {
                tagsDictionary.Add(ControllerTagKey, controllerName);
            }
            if (actionName != null)
            {
                tagsDictionary.Add(ActionTagKey, actionName);
            }
            if (source != null)
            {
                tagsDictionary.Add(SourceTagKey, source);
            }

            return tagsDictionary.FromDictionary();
        }

        private bool IsErrorStatusCode(int statusCode)
        {
            return 400 <= statusCode && statusCode <= 599;
        }
    }
}
