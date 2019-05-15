using System;
using Microsoft.AspNetCore.Http;
using DotNETDevOps.Extensions.AzureFunctions;
using Microsoft.Azure.WebJobs.Hosting;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.Options;



namespace DotNETDevOps.FrontDoor.RouterFunction
{
    public class RouteMatcher
    {
        private RouteOptions routeConfiguration;
        // private List<BaseRoute> routes = new List<BaseRoute>();
        private Dictionary<string, BaseRoute[]> routes;


        public RouteMatcher(RouteOptions routes)
        {
            this.routeConfiguration = routes;

            var locations = this.routeConfiguration.Servers.SelectMany(k => k.Locations).ToList();
            foreach(var server in routeConfiguration.Servers)
            {
                foreach(var location in server.Locations)
                {
                    location.SetServer(routeConfiguration.Upstreams,server);
                }
            }

            this.routes = new Dictionary<string, BaseRoute[]>( routeConfiguration.Servers
                .SelectMany(k => k.Hostnames.Select(h => new { hostname = h, server = k }))
                .ToLookup(k => k.hostname, v => v.server).ToDictionary(k=>k.Key,v=>v.SelectMany(k=>k.Locations).OrderBy(k=>k.Precedence).ToArray()),StringComparer.OrdinalIgnoreCase);




         //   this.routes.AddRange(locations.OfType<ExactRoute>());
         //   this.routes.AddRange(locations.OfType<PrefixRoute>().Where(k => k.StopOnMatch));
         //   this.routes.AddRange(locations.OfType<PrefixRoute>().Where(k => !k.StopOnMatch));
         //   this.routes.AddRange(locations.OfType<RegexRoute>());
            

        }

        internal BaseRoute FindMatch(HttpContext arg)
        {
            BaseRoute found = null;

            if (routes.ContainsKey(arg.Request.Host.Host))
            {


                foreach (var route in routes[arg.Request.Host.Host])
                { 
                    if (route.IsMatch(arg.Request.Path))
                    {
                        if (route.StopOnMatch)
                            return route;

                        found = route;
                    }
                }

            }

            return found;
        }
    }
}
