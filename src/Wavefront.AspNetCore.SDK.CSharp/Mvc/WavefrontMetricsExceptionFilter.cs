using Microsoft.AspNetCore.Mvc.Filters;
using Wavefront.AspNetCore.SDK.CSharp.Common;

namespace Wavefront.AspNetCore.SDK.CSharp.Mvc
{
    /// <summary>
    ///     An <see cref="IExceptionFilter"/> that records server errors for generating
    ///     Wavefront metrics, histograms, and spans.
    /// </summary>
    public class WavefrontMetricsExceptionFilter : IExceptionFilter
    {
        public void OnException(ExceptionContext context)
        {
            context.HttpContext.Items[Constants.ExceptionKey] = context.Exception;
        }
    }
}
