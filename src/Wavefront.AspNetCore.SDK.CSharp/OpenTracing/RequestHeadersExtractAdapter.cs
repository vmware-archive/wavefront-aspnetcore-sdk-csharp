// From https://github.com/opentracing-contrib/csharp-netcore/blob/master/src/OpenTracing.Contrib.NetCore/AspNetCore/RequestHeadersExtractAdapter.cs

using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using OpenTracing.Propagation;

namespace Wavefront.AspNetCore.SDK.CSharp.OpenTracing
{
    /// <summary>
    ///     ITextMap implementation that allows a tracer to extract key-value pairs from
    ///     request headers (which are stored in an IHeaderDictionary).
    /// </summary>
    public class RequestHeadersExtractAdapter : ITextMap
    {
        private readonly IHeaderDictionary _headers;

        public RequestHeadersExtractAdapter(IHeaderDictionary headers)
        {
            _headers = headers ?? throw new ArgumentNullException(nameof(headers));
        }

        public void Set(string key, string value)
        {
            throw new NotSupportedException(
                GetType().Name + " should only be used with ITracer.Extract()");
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            foreach (var entry in _headers)
            {
                yield return new KeyValuePair<string, string>(entry.Key, entry.Value);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
