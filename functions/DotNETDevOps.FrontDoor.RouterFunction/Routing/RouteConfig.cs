using Newtonsoft.Json;
using DotNETDevOps.Extensions.AzureFunctions;
using Microsoft.Azure.WebJobs.Hosting;
using System;
using Microsoft.AspNetCore.Http;
using ProxyKit;

namespace DotNETDevOps.FrontDoor.RouterFunction
{
    [JsonConverter(typeof(RouteConfigConverter))]
    public abstract class BaseRoute
    {
        public abstract int Precedence { get; }
        public string Route { get; set; }

        [JsonProperty("proxy_pass")]
        public string ProxyPass { get; set; }
       // public string[] Hostnames { get; set; } = new string[0];

        public abstract bool IsMatch(string url);

        public bool StopOnMatch { get; protected set; }

        public abstract void Initialize();

        private Server server;
        private System.Collections.Generic.Dictionary<string, Upstream> upstreams;
        internal void SetServer(System.Collections.Generic.Dictionary<string, Upstream> upstreams, Server server)
        {
            if (upstreams == null)
            {
                throw new ArgumentNullException(nameof(upstreams));
            }

            if (server == null)
            {
                throw new ArgumentNullException(nameof(server));
            }
            this.server = server;
            this.upstreams = upstreams;
        }
        public virtual void RewriteUrl(HttpContext context)
        {

        }
        public ForwardContext Forward(HttpContext context)
        {
            var url = new Uri(ProxyPass);
            var forwardUrl = ProxyPass;
            if (upstreams.ContainsKey(url.Host))
            {
                forwardUrl = forwardUrl.Replace(url.Host, upstreams[url.Host].GetUpstreamHost());
            }

            RewriteUrl(context);

            
          
            var forwarded = context
                    .ForwardTo(forwardUrl)
                    .CopyXForwardedHeaders()
                    .AddXForwardedHeaders()
                    .ApplyCorrelationId();

            return forwarded;
        }
    }
}
