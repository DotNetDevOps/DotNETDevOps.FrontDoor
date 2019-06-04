using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;

namespace DotNETDevOps.FrontDoor.RouterApp
{
    public interface IRouteOptionsFactory
    {
        Dictionary<string, BaseRoute[]> GetRoutes();

    }
    public class FileSystemRouteOptionsFactory : DefaultRouteOptionsFactory
    {
        private readonly IHostingEnvironment hostingEnvironment;

        public FileSystemRouteOptionsFactory(ILogger<FileSystemRouteOptionsFactory> logger, IHostingEnvironment hostingEnvironment): base(logger) { 
            this.hostingEnvironment = hostingEnvironment;
        }
        public override JToken GetRouteConfiguration()
        {
            var routes = JToken.Parse(File.ReadAllText(Path.Combine(this.hostingEnvironment.ContentRootPath, $"routes.{hostingEnvironment.EnvironmentName.ToLower()}.json".Replace(".production", ""))));

            return routes;
        }
    }

    public class RemoteRouteOptionsFactory : DefaultRouteOptionsFactory
    {
        private readonly IHttpClientFactory httpClientFactory;
        private readonly IConfiguration configuration;

        public RemoteRouteOptionsFactory(ILogger<FileSystemRouteOptionsFactory> logger, IHttpClientFactory httpClientFactory, IConfiguration configuration) : base(logger)
        {
            this.httpClientFactory = httpClientFactory;
            this.configuration = configuration;
        }
        public override JToken GetRouteConfiguration()
        {
            var http = httpClientFactory.CreateClient();
            var data = http.GetStringAsync(configuration.GetValue<string>("RemoteConfiguration")).GetAwaiter().GetResult();
            var routes = JToken.Parse(data);

            return routes;
        }
    }

    public abstract class DefaultRouteOptionsFactory : IRouteOptionsFactory
    {
        private readonly ILogger logger;
      

        public DefaultRouteOptionsFactory(ILogger logger)
        {
            this.logger = logger;
          
        }

        public abstract JToken GetRouteConfiguration();



        public Dictionary<string, BaseRoute[]> GetRoutes()
        {

            var routes = GetRouteConfiguration();
           


           // var ex = new ExpressionParser(Options.Create(new ExpressionParserOptions { ThrowOnError = false, Document = routes }), logger, this);


           // Recursive(ex, routes);

            var routeConfiguration = routes.ToObject<RouteOptions>();


            var locations = routeConfiguration.Servers.SelectMany(k => k.Locations).ToList();
            foreach (var server in routeConfiguration.Servers)
            {
                foreach (var location in server.Locations)
                {
                    location.SetServer(routeConfiguration.Upstreams, server);
                }
            }

           return new Dictionary<string, BaseRoute[]>(routeConfiguration.Servers
                .SelectMany(k => k.Hostnames.Select(h => new { hostname = h, server = k }))
                .ToLookup(k => k.hostname, v => v.server).ToDictionary(k => k.Key, v => v.SelectMany(k => k.Locations).OrderBy(k => k.Precedence).ThenBy(k => k.RelativePrecedence).ToArray()), StringComparer.OrdinalIgnoreCase);


        }
    }
}
