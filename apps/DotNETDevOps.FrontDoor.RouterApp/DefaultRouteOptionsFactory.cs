using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DotNETDevOps.FrontDoor.RouterApp
{
    public abstract class DefaultRouteOptionsFactory : IRouteOptionsFactory
    {
        private readonly ILogger logger;
        private readonly AsyncExpiringLazy<Dictionary<string, BaseRoute[]>> routeFactory;
       

        public DefaultRouteOptionsFactory(ILogger logger, HealthCheckRunner healthCheckRunner)
        {
            this.logger = logger;

            this.routeFactory = new AsyncExpiringLazy<Dictionary<string, BaseRoute[]>>(async (old) =>
            {


                try
                {
                    // var ex = new ExpressionParser(Options.Create(new ExpressionParserOptions { ThrowOnError = false, Document = routes }), logger, this);

                    var routes = await GetRouteConfiguration();
                    // Recursive(ex, routes);

                    var routeConfiguration = routes.ToObject<RouteOptions>();

                    await healthCheckRunner.ClearAsync();

                    var locations = routeConfiguration.Servers.SelectMany(k => k.Locations).ToList();
                    foreach (var server in routeConfiguration.Servers)
                    {
                        foreach (var location in server.Locations)
                        {
                            location.SetServer(routeConfiguration.Upstreams, server);

                            if(location.HealthCheck != null)
                            {
                                healthCheckRunner.AddHealthCheckTask(location.HealthCheck.GetHeathCheckItem(location));
                            }
                        }
                    } 


                    return new ExpirationMetadata<Dictionary<string, BaseRoute[]>>
                    {
                        Result = new Dictionary<string, BaseRoute[]>(routeConfiguration.Servers
                         .SelectMany(k => k.Hostnames.Select(h => new { hostname = h, server = k }))
                         .ToLookup(k => k.hostname, v => v.server).ToDictionary(k => k.Key, v => v.SelectMany(k => k.Locations).OrderBy(k => k.Precedence).ThenBy(k => k.RelativePrecedence).ToArray()), StringComparer.OrdinalIgnoreCase),
                        ValidUntil = DateTimeOffset.MaxValue

                    };

                }catch(Exception ex)
                {
                    return new ExpirationMetadata<Dictionary<string, BaseRoute[]>>
                    {
                        Result = new Dictionary<string, BaseRoute[]>(),
                        ValidUntil = DateTimeOffset.UtcNow.AddSeconds(5)
                   
                    };
                }
            
            });


        }

        public abstract Task<JToken> GetRouteConfiguration();

        
        protected Task Invalidate()
        {
            return this.routeFactory.Invalidate();
        }
        
        public virtual Dictionary<string, BaseRoute[]> GetRoutes()
        {

            var routeTask = routeFactory.Value();
            if (routeTask.IsCompleted)
                return routeTask.Result;

            Task.WaitAll(routeTask);

            return routeTask.Result;  
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return routeFactory.Value();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
