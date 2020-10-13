using System;
using System.Collections.Generic;
using System.Reflection;
using App.Metrics;
using App.Metrics.Reporting.Wavefront.Builder;
using Wavefront.SDK.CSharp.Common;
using Wavefront.SDK.CSharp.Common.Application;
using Wavefront.SDK.CSharp.Common.Metrics;
using static Wavefront.AspNetCore.SDK.CSharp.Common.Constants;
using static Wavefront.SDK.CSharp.Common.Constants;

namespace Wavefront.AspNetCore.SDK.CSharp.Common
{
    /// <summary>
    ///     Wavefront reporter for ASP.NET Core application.
    /// </summary>
    public class WavefrontAspNetCoreReporter
    {
        /// <summary>
        ///     Gets the App Metrics registry/reporter instance that is used to report metrics
        ///     and histograms.
        /// </summary>
        /// <value>The App Metrics registry/reporter.</value>
        public IMetricsRoot Metrics { get; }

        /// <summary>
        ///     Get the Wavefront sender instance that handles the sending of data to Wavefront,
        ///     via either Wavefront proxy or direct ingestion.
        /// </summary>
        /// <value>The Wavefront sender.</value>
        public IWavefrontSender WavefrontSender { get; }

        /// <summary>
        ///     Gets the application's metadata tags.
        /// </summary>
        /// <value>The application tags.</value>
        public ApplicationTags ApplicationTags { get; }

        /// <summary>
        ///     Gets the name of the source/host where your application is running.
        /// </summary>
        /// <value>The source/host name.</value>
        public string Source { get; }

        private WavefrontSdkMetricsRegistry sdkMetricsRegistry;

        private WavefrontAspNetCoreReporter(IMetricsRoot metrics, IWavefrontSender wavefrontSender,
                                            ApplicationTags applicationTags, string source)
        {
            Metrics = metrics;
            WavefrontSender = wavefrontSender;
            ApplicationTags = applicationTags;
            Source = source;
            sdkMetricsRegistry = new WavefrontSdkMetricsRegistry
                .Builder(wavefrontSender)
                .Prefix(SdkMetricPrefix + ".asp_net")
                .Source(source)
                .Tags(applicationTags.ToPointTags())
                .Build();
            double sdkVersion = Utils.GetSemVer(Assembly.GetExecutingAssembly());
            sdkMetricsRegistry.Gauge("version", () => sdkVersion);
        }

        public class Builder
        {
            // Required parameters
            private readonly ApplicationTags applicationTags;
            private string source;

            // Optional parameters
            private int reportingIntervalSeconds = 60;

            /// <summary>
            ///     Initializes a Builder for <see cref="WavefrontAspNetCoreReporter"/>.
            /// </summary>
            /// <param name="applicationTags">
            ///     Metadata about your application that is propagated as tags when
            ///     metrics/histograms are sent to Wavefront.
            /// </param>
            public Builder(ApplicationTags applicationTags)
            {
                this.applicationTags = applicationTags;
            }

            /// <summary>
            ///     Sets the reporting interval (i.e. how often you want to report
            ///     metrics/histograms to Wavefront). 
            /// </summary>
            /// <returns><see cref="this"/></returns>
            /// <param name="reportingIntervalSeconds">The reporting interval in seconds.</param>
            public Builder ReportingIntervalSeconds(int reportingIntervalSeconds)
            {
                this.reportingIntervalSeconds = reportingIntervalSeconds;
                return this;
            }

            /// <summary>
            ///     Sets the source tag for metrics and histograms.
            /// </summary>
            /// <returns><see cref="this"/></returns>
            /// <param name="source">
            ///     Name of the source/host where your application is running.
            /// </param>
            public Builder WithSource(string source)
            {
                this.source = source;
                return this;
            }

            /// <summary>
            ///     Builds a <see cref="WavefrontAspNetCoreReporter"/>.
            /// </summary>
            /// <returns>The <see cref="WavefrontAspNetCoreReporter"/> instance.</returns>
            /// <param name="wavefrontSender">
            ///     The Wavefront sender instance that handles the sending of data to Wavefront,
            ///     via either Wavefront proxy or direct ingestion.
            /// </param>
            public WavefrontAspNetCoreReporter Build(IWavefrontSender wavefrontSender)
            {
                source = string.IsNullOrWhiteSpace(source) ? Utils.GetDefaultSource() : source;

                var globalTags = new Dictionary<string, string>
                {
                    { ApplicationTagKey, applicationTags.Application }
                };
                if (applicationTags.CustomTags != null)
                {
                    foreach (var customTag in applicationTags.CustomTags)
                    {
                        if (!globalTags.ContainsKey(customTag.Key))
                        {
                            globalTags.Add(customTag.Key, customTag.Value);
                        }
                    }
                }

                var metrics = new MetricsBuilder()
                    .Configuration.Configure(
                        options =>
                        {
                            options.DefaultContextLabel = AspNetCoreContext;
                            options.GlobalTags = new GlobalMetricTags(globalTags);
                        })
                    .Report.ToWavefront(
                        options =>
                        {
                            options.WavefrontSender = wavefrontSender;
                            options.Source = source;
                            options.WavefrontHistogram.ReportMinuteDistribution = true;
                            options.FlushInterval = TimeSpan.FromSeconds(reportingIntervalSeconds);
                        })
                    .Build();

                return new WavefrontAspNetCoreReporter(
                    metrics, wavefrontSender, applicationTags, source);
            }
        }
    }
}
