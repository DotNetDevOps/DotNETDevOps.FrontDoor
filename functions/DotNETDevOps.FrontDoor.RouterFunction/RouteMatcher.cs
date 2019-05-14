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
        private List<BaseRoute> routes = new List<BaseRoute>();

        public RouteMatcher(RouteOptions routes)
        {
            this.routeConfiguration = routes;

            this.routes.AddRange(this.routeConfiguration.Routes.OfType<ExactRoute>());
            this.routes.AddRange(this.routeConfiguration.Routes.OfType<PrefixRoute>().Where(k => k.StopOnMatch));
            this.routes.AddRange(this.routeConfiguration.Routes.OfType<PrefixRoute>().Where(k => !k.StopOnMatch));
            this.routes.AddRange(this.routeConfiguration.Routes.OfType<RegexRoute>());
            

        }

        internal BaseRoute FindMatch(HttpContext arg)
        {
            BaseRoute found = null;
            foreach (var route in routes)
            {
                if (!route.Hostnames.Contains(arg.Request.Host.Host,StringComparer.OrdinalIgnoreCase))
                    continue;

                if (route.IsMatch(arg.Request.Path))
                {
                    if(route.StopOnMatch)
                        return route;

                    found = route;
                }
            }
             

            return found;
        }
    }
}
