using Microsoft.AspNetCore.Http;

namespace Wavefront.AspNetCore.SDK.CSharp.Common
{
    /// <summary>
    ///     A utils class to generate metric names for ASP.NET Core application requests/responses.
    /// </summary>
    internal abstract class MetricNameUtils
    {
        private static readonly string RequestPrefix = "request.";
        private static readonly string ResponsePrefix = "response.";

        /// <summary>
        ///     Util to generate metric name from the HTTP request and the route template.
        /// </summary>
        /// <returns>The generated metric name.</returns>
        /// <param name="request">The HTTP request object.</param>
        /// <param name="routeTemplate">The route template.</param>
        internal static string MetricName(HttpRequest request, string routeTemplate)
        {
            return MetricName(request, routeTemplate, RequestPrefix);
        }

        /// <summary>
        ///     Util to generate metric name from the HTTP response and the route template.
        /// </summary>
        /// <returns>The generated metric name.</returns>
        /// <param name="request">The HTTP request object.</param>
        /// <param name="routeTemplate">The route template.</param>
        /// <param name="response">The HTTP response object.</param>
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
