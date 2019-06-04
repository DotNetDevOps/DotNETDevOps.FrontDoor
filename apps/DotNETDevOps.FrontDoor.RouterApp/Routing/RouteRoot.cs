using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace DotNETDevOps.FrontDoor.RouterApp
{

    public class UpstreamHostServer
    {
        public string Host { get; set; }
        public uint Weight { get; set; } = 1;

        public JObject Extensions { get; set; }
    }
    public class Upstream
    {
        public UpstreamHostServer[] Servers { get; set; }
   //     public Lazy<RoundRobin> RoundRobin;
        public Upstream()
        {
           // RoundRobin = new Lazy<RoundRobin>(() => new RoundRobin(Servers.Select(s => new UpstreamHost(s.Host, s.Weight)).ToArray()));
        }

        internal UpstreamHostServer GetUpstreamHost()
        {
            var server = Servers.First();

            return server;
           
        }
    }
    //public class ServerLocation
    //{
    //    public [] Routes{get;set;}
    //}
    public class Server
    {
        public BaseRoute[] Locations { get; set; }
        public string[] Hostnames { get; set; }
    }
    public class RouteOptions
    {
        public Dictionary<string, Upstream> Upstreams { get; set; } = new Dictionary<string, Upstream>();
        public Server[] Servers { get; set; } = new Server[0];
      
    }
}
