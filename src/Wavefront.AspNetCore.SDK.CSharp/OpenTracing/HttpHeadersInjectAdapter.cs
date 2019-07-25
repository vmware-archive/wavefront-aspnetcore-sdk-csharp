// From https://github.com/opentracing-contrib/csharp-netcore/blob/master/src/OpenTracing.Contrib.NetCore/CoreFx/HttpHeadersInjectAdapter.cs

using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http.Headers;
using OpenTracing.Propagation;

namespace Wavefront.AspNetCore.SDK.CSharp.OpenTracing
{
    /// <summary>
    /// Injects carrier data into HTTP headers.
    /// </summary>
    public class HttpHeadersInjectAdapter : ITextMap
    {
        private readonly HttpHeaders _headers;

        public HttpHeadersInjectAdapter(HttpHeaders headers)
        {
            _headers = headers ?? throw new ArgumentNullException(nameof(headers));
        }

        public void Set(string key, string value)
        {
            if (_headers.Contains(key))
            {
                _headers.Remove(key);
            }

            _headers.Add(key, value);
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            throw new NotSupportedException(
                GetType().Name + " should only be used with ITracer.Inject");
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
