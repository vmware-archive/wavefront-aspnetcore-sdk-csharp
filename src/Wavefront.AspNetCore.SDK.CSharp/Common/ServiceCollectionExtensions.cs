using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTracing;
using OpenTracing.Util;
using Wavefront.AspNetCore.SDK.CSharp.Diagnostics;

namespace Wavefront.AspNetCore.SDK.CSharp.Common
{
    /// <summary>
    ///     Extension methods for <see cref="IServiceCollection"/> to enable out-of-the-box
    ///     Wavefront metrics and reporting for ASP.NET Core applications.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        ///     Enables out-of-the-box Wavefront metrics and reporting for an ASP.NET Core MVC
        ///     application.
        /// </summary>
        /// <returns>The <see cref="IServiceCollection"/> instance.</returns>
        /// <param name="services">The <see cref="IServiceCollection"/> instance.</param>
        /// <param name="wfAspNetCoreReporter">The Wavefront ASP.NET Core reporter.</param>
        public static IServiceCollection AddWavefrontForMvc(
            this IServiceCollection services,
            WavefrontAspNetCoreReporter wfAspNetCoreReporter)
        {
            return services.AddWavefrontForMvc(wfAspNetCoreReporter, null);
        }

        /// <summary>
        ///     Enables out-of-the-box Wavefront metrics, tracing, and reporting for an ASP.NET Core MVC
        ///     application.
        /// </summary>
        /// <returns>The <see cref="IServiceCollection"/> instance.</returns>
        /// <param name="services">The <see cref="IServiceCollection"/> instance.</param>
        /// <param name="wfAspNetCoreReporter">The Wavefront ASP.NET Core reporter.</param>
        /// <param name="tracer">The Wavefront tracer.</param>
        public static IServiceCollection AddWavefrontForMvc(
            this IServiceCollection services,
            WavefrontAspNetCoreReporter wfAspNetCoreReporter,
            ITracer tracer)
        {
            // register App Metrics registry and services
            services.AddMetrics(wfAspNetCoreReporter.Metrics);

            // register App Metrics reporting scheduler
            services.AddMetricsReportScheduler();

            // register Wavefront ASP.NET Core reporter
            services.TryAddSingleton(wfAspNetCoreReporter);

            // register tracer
            if (tracer != null)
            {
                GlobalTracer.Register(tracer);
            }
            services.TryAddSingleton(GlobalTracer.Instance);

            // register Wavefront Heartbeater hosted service
            services.AddHostedService<HeartbeaterHostedService>();

            // register Wavefront instrumentation services
            services.TryAddSingleton<DiagnosticManager>();
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<DiagnosticObserver, AspNetCoreDiagnostics>());
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<DiagnosticObserver, HttpHandlerDiagnostics>());
            services.AddHostedService<InstrumentationService>();

            return services;
        }
    }
}
