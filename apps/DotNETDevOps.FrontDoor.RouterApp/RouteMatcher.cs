using System;
using Microsoft.AspNetCore.Http;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.Options;



namespace DotNETDevOps.FrontDoor.RouterApp
{
    public class RouteMatcher
    {
        private readonly IRouteOptionsFactory factory;

        //private RouteOptions routeConfiguration;
        // private List<BaseRoute> routes = new List<BaseRoute>();
        //   private Dictionary<string, BaseRoute[]> routes;


        public RouteMatcher(IRouteOptionsFactory factory)
        {
            this.factory = factory;






            //   this.routes.AddRange(locations.OfType<ExactRoute>());
            //   this.routes.AddRange(locations.OfType<PrefixRoute>().Where(k => k.StopOnMatch));
            //   this.routes.AddRange(locations.OfType<PrefixRoute>().Where(k => !k.StopOnMatch));
            //   this.routes.AddRange(locations.OfType<RegexRoute>());


        }

        internal BaseRoute FindMatch(HttpContext arg)
        {
            var routes = factory.GetRoutes();

            if (routes.ContainsKey("*") && FindMatch(arg, routes["*"], out var route))
                return route;

            if (routes.ContainsKey(arg.Request.Host.Host) && FindMatch(arg, routes[arg.Request.Host.Host], out route ))
                return route;

            //*.forms.io-board.com | test.forms.io-board.com
            foreach (var host in routes.Where(k => k.Key.StartsWith("*") && arg.Request.Host.Host.EndsWith(k.Key.Substring(1))))
            {
                if (FindMatch(arg, host.Value, out route))
                    return route;
            }
            

            return null;
        }

        private bool FindMatch(HttpContext arg, BaseRoute[] routeCollection, out BaseRoute found)
        {
            found = null;

            foreach (var route in from route in routeCollection
                                  where route.IsMatch(arg.Request.Path)
                                  select route)
            {
                found = route;

                if (route.StopOnMatch)
                    return true;
             
            }


            return found != null;
            
        }
    }
}
