using System.Collections.Generic;

namespace Wavefront.AspNetCore.SDK.CSharp
{
    public class ApplicationTags
    {
        public string Application { get; private set; }
        public string Cluster { get; private set; }
        public string Service { get; private set; }
        public string Shard { get; private set; }
        public IDictionary<string, string> CustomTags { get; private set; }

        private ApplicationTags()
        {
        }

        public class Builder
        {
            private readonly string application;
            private readonly string service;
            private string cluster;
            private string shard;
            private IDictionary<string, string> customTags = new Dictionary<string, string>();

            public Builder(string application, string service)
            {
                this.application = application;
                this.service = service;
            }

            public Builder Cluster(string cluster)
            {
                this.cluster = cluster;
                return this;
            }

            public Builder Shard(string shard)
            {
                this.shard = shard;
                return this;
            }

            public Builder CustomTags(IDictionary<string, string> customTags)
            {
                this.customTags = customTags;
                return this;
            }

            public ApplicationTags Build()
            {
                return new ApplicationTags()
                {
                    Application = application,
                    Cluster = cluster,
                    Service = service,
                    Shard = shard,
                    CustomTags = customTags
                };
            }
        }
    }
}
