using System;
using Microsoft.AspNetCore.Http;
using DotNETDevOps.Extensions.AzureFunctions;
using Microsoft.Azure.WebJobs.Hosting;
using System.Linq;
using System.Collections.Generic;

[assembly: WebJobsStartup(typeof(AspNetCoreWebHostStartUp))]
[assembly: WebJobsStartup(typeof(DotNETDevOps.FrontDoor.RouterFunction.WebHostBuilderConfigurationBuilderExtension))]

namespace DotNETDevOps.FrontDoor.RouterFunction
{
    public class RouteMatcher
    {
        private RouteRoot routeConfiguration;
        private List<RouteConfig> routes = new List<RouteConfig>();

        public RouteMatcher(RouteRoot routes)
        {
            this.routeConfiguration = routes;
            this.routes.AddRange(routes.Routes.OfType<ExactRoute>());
            this.routes.AddRange(routes.Routes.OfType<PrefixRoute>().Where(k => k.StopOnMatch));
            this.routes.AddRange(routes.Routes.OfType<PrefixRoute>().Where(k => !k.StopOnMatch));
            this.routes.AddRange(routes.Routes.OfType<RegexRoute>());
            

        }

        internal RouteConfig FindMatch(HttpContext arg)
        {
            RouteConfig found = null;
            foreach (var route in routes)
            {
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
