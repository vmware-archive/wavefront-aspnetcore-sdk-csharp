using System.Collections.Generic;
using App.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Wavefront.CSharp.SDK.Entities.Metrics;

namespace Wavefront.AspNetCore.SDK.CSharp
{
    public static class WavefrontServiceCollectionExtensions
    {
        public static IServiceCollection AddWavefrontMetrics(
            this IServiceCollection services,
            IMetricsRoot metrics,
            IWavefrontMetricSender wavefrontMetricSender,
            ApplicationTags applicationTags,
            string source)
        {
            services.AddMetrics(metrics);
            services.AddMetricsReportScheduler();
            services.Configure<WavefrontReportingOptions>(options => {
                options.Component = Constants.AspNetCoreComponent;
                options.Source = source;
            });
            services.AddSingleton(wavefrontMetricSender);
            services.AddSingleton(applicationTags);
            services.AddHostedService<HeartbeaterService>();
            services.AddSingleton<WavefrontMetricsResourceFilter>();
            services.AddMvc(options =>
            {
                options.Filters.AddService<WavefrontMetricsResourceFilter>();
            });
            return services;
        }
    }
}
