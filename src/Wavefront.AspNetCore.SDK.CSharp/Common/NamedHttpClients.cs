namespace Wavefront.AspNetCore.SDK.CSharp.Common
{
    /// <summary>
    ///     Names of named clients that are registered and can be built using HttpClientFactory.
    /// </summary>
    public static class NamedHttpClients
    {
        /// <summary>
        ///     Name of named client that automatically takes care of cross-process span context
        ///     propagation.
        /// </summary>
        public static readonly string SpanContextPropagationClient = "SpanContextPropagationClient";
    }
}
