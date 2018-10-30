using App.Metrics;

namespace Wavefront.AspNetCore.SDK.CSharp
{
    public static class Constants
    {
        public static readonly string HeartbeatMetric = "~component.heartbeat";

        public static readonly string WavefrontProvidedSource = "wavefront-provided";

        public static readonly string NullTagValue = "none";

        public static readonly string SourceTagKey = "source";

        public static readonly string ApplicationTagKey = "application";

        public static readonly string ClusterTagKey = "cluster";

        public static readonly string ServiceTagKey = "service";

        public static readonly string ShardTagKey = "shard";

        public static readonly string ComponentTagKey = "component";

        public static readonly string AspNetCoreComponent = "AspNetCore";

        public static readonly string AspNetCoreContext = "AspNetCore";

        public static readonly string ControllerTagKey = AspNetCoreContext + ".resource.controller";

        public static readonly string ActionTagKey = AspNetCoreContext + ".resource.action";

        public static readonly Unit ResponseUnit = Unit.Custom("resp");

        public static readonly Unit MillisecondUnit = Unit.Custom("ms");
        
        public static readonly Unit NanosecondUnit = Unit.Custom("ns");
    }
}
