using Microsoft.Extensions.DependencyInjection;
using Wavefront.AspNetCore.SDK.CSharp.Mvc;

namespace Wavefront.AspNetCore.SDK.CSharp.Common
{
    /// <summary>
    ///     Extension methods for <see cref="IServiceCollection"/> to enable out-of-the-box
    ///     Wavefront metrics and reporting for ASP.NET Core applications.
    /// </summary>
    public static class WavefrontServiceCollectionExtensions
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
            services.AddMetrics(wfAspNetCoreReporter.Metrics);
            services.AddMetricsReportScheduler();
            services.AddSingleton(wfAspNetCoreReporter);
            services.AddHostedService<HeartbeaterHostedService>();
            services.AddSingleton<WavefrontMetricsResourceFilter>();
            services.AddMvc(options =>
            {
                options.Filters.AddService<WavefrontMetricsResourceFilter>();
            });
            return services;
        }
    }
}
