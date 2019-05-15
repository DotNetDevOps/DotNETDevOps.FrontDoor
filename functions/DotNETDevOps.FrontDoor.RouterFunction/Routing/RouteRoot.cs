using DotNETDevOps.Extensions.AzureFunctions;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Hosting;
using ProxyKit;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DotNETDevOps.FrontDoor.RouterFunction
{
   
    public class UpstreamHostServer
    {
        public string Host { get; set; }
        public uint Weight { get; set; } = 1;
    }
    public class Upstream
    {
        public UpstreamHostServer[] Servers { get; set; }
   //     public Lazy<RoundRobin> RoundRobin;
        public Upstream()
        {
           // RoundRobin = new Lazy<RoundRobin>(() => new RoundRobin(Servers.Select(s => new UpstreamHost(s.Host, s.Weight)).ToArray()));
        }

        internal string GetUpstreamHost()
        {
            var server = Servers.First();

            return server.Host;
           
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
