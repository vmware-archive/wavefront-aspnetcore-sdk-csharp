// From https://github.com/opentracing-contrib/csharp-netcore/blob/master/src/OpenTracing.Contrib.NetCore/Internal/SpanExtensions.cs

using System;
using System.Collections.Generic;
using OpenTracing;
using OpenTracing.Tag;

namespace Wavefront.AspNetCore.SDK.CSharp.OpenTracing
{
    public static class SpanExtensions
    {
        /// <summary>
        /// Adds information about the <paramref name="exception"/> to the given <paramref name="span"/>.
        /// </summary>
        public static void SetException(this ISpan span, Exception exception)
        {
            if (span == null || exception == null)
                return;

            span.Log(new Dictionary<string, object>(3)
            {
                { LogFields.Event, Tags.Error.Key },
                { LogFields.ErrorKind, exception.GetType().Name },
                { LogFields.ErrorObject, exception }
            });
        }
    }
}
