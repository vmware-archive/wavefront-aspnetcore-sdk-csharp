using Microsoft.AspNetCore.Http;

namespace Wavefront.AspNetCore.SDK.CSharp
{
    internal abstract class MetricNameUtils
    {
        private static readonly string RequestPrefix = "request.";
        private static readonly string ResponsePrefix = "response.";

        internal static string MetricName(HttpRequest request, string routeTemplate)
        {
            return MetricName(request, routeTemplate, RequestPrefix);
        }

        internal static string MetricName(
            HttpRequest request, string routeTemplate, HttpResponse response)
        {
            return $"{MetricName(request, routeTemplate, ResponsePrefix)}.{response.StatusCode}";
        }

        private static string MetricName(HttpRequest request, string routeTemplate, string prefix)
        {
            return prefix + MetricName(request.Method, routeTemplate);
        }

        private static string MetricName(string httpMethod, string routeTemplate)
        {
            string metricId = StripLeadingAndTrailingSlashes(routeTemplate);
            metricId = metricId.Replace('/', '.')
                               .Replace(":", "")
                               .Replace("{", "_")
                               .Replace("}", "_");

            return $"{metricId}.{httpMethod}";
        }

        private static string StripLeadingAndTrailingSlashes(string routeTemplate)
        {
            return routeTemplate == null ? "" : routeTemplate.Trim('/');
        }
    }
}
