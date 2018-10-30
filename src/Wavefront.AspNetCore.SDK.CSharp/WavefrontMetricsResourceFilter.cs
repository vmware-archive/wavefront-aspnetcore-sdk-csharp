using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using App.Metrics;
using App.Metrics.Counter;
using App.Metrics.Histogram;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Wavefront.AspNetCore.SDK.CSharp
{
    public class WavefrontMetricsResourceFilter : IResourceFilter
    {
        private readonly ILogger<WavefrontMetricsResourceFilter> logger;
        private readonly IMetrics metrics;
        private readonly ApplicationTags applicationTags;
        private readonly string source;

        private readonly MetricTags overallAggregatedPerSourceTags;
        private readonly MetricTags overallAggregatedPerShardTags;
        private readonly MetricTags overallAggregatedPerServiceTags;
        private readonly MetricTags overallAggregatedPerClusterTags;
        private readonly MetricTags overallAggregatedPerApplicationTags;

        private readonly WavefrontGaugeOptions totalInflightRequestGauge;

        private readonly ConcurrentDictionary<WavefrontGaugeOptions, StrongBox<int>> gauges =
            new ConcurrentDictionary<WavefrontGaugeOptions, StrongBox<int>>();

        public WavefrontMetricsResourceFilter(
            ILogger<WavefrontMetricsResourceFilter> logger,
            IMetrics metrics,
            ApplicationTags applicationTags,
            IOptions<WavefrontReportingOptions> wavefrontReportingOptions)
        {
            this.logger = logger;
            this.metrics = metrics;
            this.applicationTags = applicationTags;
            source = wavefrontReportingOptions.Value.Source;

            overallAggregatedPerSourceTags = GetTags(true, true, true, null, null, null);
            overallAggregatedPerShardTags =
                GetTags(true, true, true, null, null, Constants.WavefrontProvidedSource);
            overallAggregatedPerServiceTags =
                GetTags(true, true, false, null, null, Constants.WavefrontProvidedSource);
            overallAggregatedPerClusterTags =
                GetTags(true, false, false, null, null, Constants.WavefrontProvidedSource);
            overallAggregatedPerApplicationTags =
                GetTags(false, false, false, null, null, Constants.WavefrontProvidedSource);

            totalInflightRequestGauge = new WavefrontGaugeOptions()
            {
                Context = Constants.AspNetCoreContext,
                Name = "total_requests.inflight",
                Tags = overallAggregatedPerSourceTags,
                MeasurementUnit = Unit.Requests
            };
        }

        public void OnResourceExecuting(ResourceExecutingContext context)
        {
            var request = context.HttpContext.Request;
            var controllerActionDescriptor = context.ActionDescriptor as ControllerActionDescriptor;
            string controllerName = controllerActionDescriptor.ControllerName;
            string actionName = controllerActionDescriptor.ActionName;
            string routeTemplate = controllerActionDescriptor.AttributeRouteInfo.Template;
            string requestMetricKey = MetricNameUtils.MetricName(request, routeTemplate);

            context.HttpContext.Items["startTimeMillis"] = GetCurrentMillis();
//          context.HttpContext.Items["startTimeNanos"] = GetCurrentNanos();

            var completeTags = GetTags(true, true, true, controllerName, actionName, null);

            /* Gauges
             * 1) AspNetCore.request.api.v2.alert.summary.GET.inflight.value
             * 2) AspNetCore.total_requests.inflight.value
             */
            var inflightRequestGauge = new WavefrontGaugeOptions()
            {
                Context = Constants.AspNetCoreContext,
                Name = requestMetricKey + ".inflight",
                Tags = completeTags,
                MeasurementUnit = Unit.Requests
            };
            Interlocked.Increment(ref GetGaugeValue(inflightRequestGauge).Value);
            Interlocked.Increment(ref GetGaugeValue(totalInflightRequestGauge).Value);
        }

        public void OnResourceExecuted(ResourceExecutedContext context)
        {
            var request = context.HttpContext.Request;
            var response = context.HttpContext.Response;
            var controllerActionDescriptor = context.ActionDescriptor as ControllerActionDescriptor;
            string controllerName = controllerActionDescriptor.ControllerName;
            string actionName = controllerActionDescriptor.ActionName;
            string routeTemplate = controllerActionDescriptor.AttributeRouteInfo.Template;
            string requestMetricKey = MetricNameUtils.MetricName(request, routeTemplate);
            string responseMetricKey = MetricNameUtils.MetricName(request, routeTemplate, response);

            var completeTags = GetTags(true, true, true, controllerName, actionName, null);

            /* 
             * Gauges
             * 1) AspNetCore.request.api.v2.alert.summary.GET.inflight.value
             * 2) AspNetCore.total_requests.inflight.value
             */
            var inflightRequestGauge = new WavefrontGaugeOptions()
            {
                Context = Constants.AspNetCoreContext,
                Name = requestMetricKey + ".inflight",
                Tags = completeTags,
                MeasurementUnit = Unit.Requests
            };
            Interlocked.Decrement(ref gauges[inflightRequestGauge].Value);
            Interlocked.Decrement(ref gauges[totalInflightRequestGauge].Value);

            var aggregatedPerShardTags = GetTags(true, true, true, controllerName, actionName,
                                                 Constants.WavefrontProvidedSource);
            var aggregatedPerServiceTags = GetTags(true, true, false, controllerName, actionName,
                                                   Constants.WavefrontProvidedSource);
            var aggregatedPerClusterTags = GetTags(true, false, false, controllerName, actionName,
                                                   Constants.WavefrontProvidedSource);
            var aggregatedPerApplicationTags = GetTags(false, false, false, controllerName, actionName,
                                                   Constants.WavefrontProvidedSource);

            /*
             * Granular response metrics
             * 1) AspNetCore.response.api.v2.alert.summary.GET.200.cumulative.count (Counter)
             * 2) AspNetCore.response.api.v2.alert.summary.GET.200.aggregated_per_shard.count (DeltaCounter)
             * 3) AspNetCore.response.api.v2.alert.summary.GET.200.aggregated_per_service.count (DeltaCounter)
             * 4) AspNetCore.response.api.v2.alert.summary.GET.200.aggregated_per_cluster.count (DeltaCounter)
             * 5) AspNetCore.response.api.v2.alert.summary.GET.200.aggregated_per_application.count (DeltaCounter)
             */
            metrics.Measure.Counter.Increment(new CounterOptions()
            {
                Context = Constants.AspNetCoreContext,
                Name = responseMetricKey + ".cumulative",
                Tags = completeTags,
                MeasurementUnit = Constants.ResponseUnit
            });
            if (applicationTags.Shard != null)
            {
                metrics.Measure.Counter.Increment(
                    new DeltaCounterOptions.Builder(responseMetricKey + ".aggregated_per_shard")
                                           .Context(Constants.AspNetCoreContext)
                                           .Tags(aggregatedPerShardTags)
                                           .MeasurementUnit(Constants.ResponseUnit)
                                           .Build());
            }
            metrics.Measure.Counter.Increment(
                new DeltaCounterOptions.Builder(responseMetricKey + ".aggregated_per_service")
                                       .Context(Constants.AspNetCoreContext)
                                       .Tags(aggregatedPerServiceTags)
                                       .MeasurementUnit(Constants.ResponseUnit)
                                       .Build());
            if (applicationTags.Cluster != null)
            {
                metrics.Measure.Counter.Increment(
                    new DeltaCounterOptions.Builder(responseMetricKey + ".aggregated_per_cluster")
                                           .Context(Constants.AspNetCoreContext)
                                           .Tags(aggregatedPerClusterTags)
                                           .MeasurementUnit(Constants.ResponseUnit)
                                           .Build());
            }
            metrics.Measure.Counter.Increment(
                new DeltaCounterOptions.Builder(responseMetricKey + ".aggregated_per_application")
                                       .Context(Constants.AspNetCoreContext)
                                       .Tags(aggregatedPerApplicationTags)
                                       .MeasurementUnit(Constants.ResponseUnit)
                                       .Build());

            /*
             * Overall error response metrics
             * 1) AspNetCore.response.errors.aggregated_per_source.count (Counter)
             * 2) AspNetCore.response.errors.aggregated_per_shard.count (DeltaCounter)
             * 3) AspNetCore.response.errors.aggregated_per_service.count (DeltaCounter)
             * 4) AspNetCore.response.errors.aggregated_per_cluster.count (DeltaCounter)
             * 5) AspNetCore.response.errors.aggregated_per_application.count (DeltaCounter)
             */
            if (400 <= response.StatusCode && response.StatusCode < 600)
            {
                metrics.Measure.Counter.Increment(new CounterOptions()
                {
                    Context = Constants.AspNetCoreContext,
                    Name = "response.errors",
                    Tags = completeTags,
                    MeasurementUnit = Unit.Errors
                });
                metrics.Measure.Counter.Increment(new CounterOptions()
                {
                    Context = Constants.AspNetCoreContext,
                    Name = "response.errors.aggregated_per_source",
                    Tags = overallAggregatedPerSourceTags,
                    MeasurementUnit = Unit.Errors
                });
                if (applicationTags.Shard != null)
                {
                    metrics.Measure.Counter.Increment(
                        new DeltaCounterOptions.Builder("response.errors.aggregated_per_shard")
                                               .Context(Constants.AspNetCoreContext)
                                               .Tags(overallAggregatedPerShardTags)
                                               .MeasurementUnit(Unit.Errors)
                                               .Build());
                }
                metrics.Measure.Counter.Increment(
                    new DeltaCounterOptions.Builder("response.errors.aggregated_per_service")
                                           .Context(Constants.AspNetCoreContext)
                                           .Tags(overallAggregatedPerServiceTags)
                                           .MeasurementUnit(Unit.Errors)
                                           .Build());
                if (applicationTags.Cluster != null)
                {
                    metrics.Measure.Counter.Increment(
                        new DeltaCounterOptions.Builder("response.errors.aggregated_per_cluster")
                                               .Context(Constants.AspNetCoreContext)
                                               .Tags(overallAggregatedPerClusterTags)
                                               .MeasurementUnit(Unit.Errors)
                                               .Build());
                }
                metrics.Measure.Counter.Increment(
                    new DeltaCounterOptions.Builder("response.errors.aggregated_per_application")
                                           .Context(Constants.AspNetCoreContext)
                                           .Tags(overallAggregatedPerApplicationTags)
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
            metrics.Measure.Counter.Increment(new CounterOptions()
            {
                Context = Constants.AspNetCoreContext,
                Name = "response.completed.aggregated_per_source",
                Tags = overallAggregatedPerSourceTags,
                MeasurementUnit = Constants.ResponseUnit
            });
            if (applicationTags.Shard != null)
            {
                metrics.Measure.Counter.Increment(
                    new DeltaCounterOptions.Builder("response.completed.aggregated_per_shard")
                                           .Context(Constants.AspNetCoreContext)
                                           .Tags(overallAggregatedPerShardTags)
                                           .MeasurementUnit(Constants.ResponseUnit)
                                           .Build());
            }
            metrics.Measure.Counter.Increment(
                new DeltaCounterOptions.Builder("response.completed.aggregated_per_service")
                                       .Context(Constants.AspNetCoreContext)
                                       .Tags(overallAggregatedPerServiceTags)
                                       .MeasurementUnit(Constants.ResponseUnit)
                                       .Build());
            if (applicationTags.Cluster != null)
            {
                metrics.Measure.Counter.Increment(
                    new DeltaCounterOptions.Builder("response.completed.aggregated_per_cluster")
                                           .Context(Constants.AspNetCoreContext)
                                           .Tags(overallAggregatedPerClusterTags)
                                           .MeasurementUnit(Constants.ResponseUnit)
                                           .Build());
            }
            metrics.Measure.Counter.Increment(
                new DeltaCounterOptions.Builder("response.completed.aggregated_per_application")
                                       .Context(Constants.AspNetCoreContext)
                                       .Tags(overallAggregatedPerApplicationTags)
                                       .MeasurementUnit(Constants.ResponseUnit)
                                       .Build());

            /*
             * WavefrontHistograms
             * 1) AspNetCore.response.api.v2.alert.summary.GET.200.latency
             */
            long apiLatency =
                GetCurrentMillis() - (long)context.HttpContext.Items["startTimeMillis"];
            metrics.Measure.Histogram.Update(
                new WavefrontHistogramOptions.Builder(responseMetricKey + ".latency")
                                       .Context(Constants.AspNetCoreContext)
                                       .Tags(completeTags)
                                       .MeasurementUnit(Constants.MillisecondUnit)
                                       .Build(), apiLatency);
/*
            long cpuNanos = GetCurrentNanos() - (long)context.HttpContext.Items["startTimeNanos"];
            metrics.Measure.Histogram.Update(
                new WavefrontHistogramOptions.Builder(responseMetricKey + ".cpu_ns")
                                       .Context(Constants.AspNetCoreContext)
                                       .Tags(completeTags)
                                       .MeasurementUnit(Constants.NanosecondUnit)
                                       .Build(), cpuNanos);
*/
        }

        private StrongBox<int> GetGaugeValue(WavefrontGaugeOptions gaugeOptions)
        {
            return gauges.GetOrAdd(gaugeOptions, key =>
            {
                StrongBox<int> toReturn = new StrongBox<int>();
                metrics.Measure.Gauge.SetValue(gaugeOptions, () => toReturn.Value);
                return toReturn;
            });
        }

        private MetricTags GetTags(
            bool includeCluster, bool includeService, bool includeShard, 
            string controllerName, string actionName, string source)
        {
            var tagsDictionary = new Dictionary<string, string>(applicationTags.CustomTags)
            {
                { Constants.ApplicationTagKey, applicationTags.Application }
            };
            if (includeCluster)
            {
                tagsDictionary.Add(
                    Constants.ClusterTagKey, applicationTags.Cluster ?? Constants.NullTagValue);
            }
            if (includeService)
            {
                tagsDictionary.Add(Constants.ServiceTagKey, applicationTags.Service);
            }
            if (includeShard)
            {
                tagsDictionary.Add(
                    Constants.ShardTagKey, applicationTags.Shard ?? Constants.NullTagValue);
            }
            if (controllerName != null)
            {
                tagsDictionary.Add(Constants.ControllerTagKey, controllerName);
            }
            if (actionName != null)
            {
                tagsDictionary.Add(Constants.ActionTagKey, actionName);
            }
            tagsDictionary.Add(Constants.SourceTagKey, source ?? this.source);

            return tagsDictionary.FromDictionary();
        }

        private static long GetCurrentMillis()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
/*
        private static long GetCurrentNanos()
        {
            return (1000000000L / Stopwatch.Frequency) * Stopwatch.GetTimestamp();
        }
*/
    }
}
