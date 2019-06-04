using DotNETDevOps.FrontDoor.AspNetCore;
using DotNETDevOps.JsonFunctions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using ProxyKit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace DotNETDevOps.FrontDoor.RouterApp
{
    public static class CorrelationIdExtensions
    {
        public const string XCorrelationId = "X-Correlation-ID";

        public static ForwardContext ApplyCorrelationId(this ForwardContext forwardContext)
        {
            if (!forwardContext.UpstreamRequest.Headers.Contains(XCorrelationId))
            {
                forwardContext.UpstreamRequest.Headers.Add(XCorrelationId, Guid.NewGuid().ToString());
            }
            return forwardContext;
        }
    }

    public class RouteOptionsFactory 
    {
        private readonly ILogger<RouteOptionsFactory> logger;
        private readonly IHostingEnvironment hostingEnvironment;

        public RouteOptionsFactory(ILogger<RouteOptionsFactory> logger, IHostingEnvironment hostingEnvironment)
        {
            this.logger = logger;
            this.hostingEnvironment = hostingEnvironment;
        }

         
        public Dictionary<string, BaseRoute[]> GetRoutes()
        {
            var routes = JToken.Parse(File.ReadAllText(Path.Combine(this.hostingEnvironment.ContentRootPath, $"routes.{hostingEnvironment.EnvironmentName.ToLower()}.json".Replace(".production", ""))));



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

    public class Startup
    {
        private readonly IHostingEnvironment hostingEnvironment;

        public Startup(IHostingEnvironment hostingEnvironment)
        {
            this.hostingEnvironment = hostingEnvironment;
        }
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddProxy();



            services.AddSingleton<RouteOptionsFactory>();
            services.AddSingleton< RouteMatcher>();

            services.AddHttpClient("ArmClient", http =>
            {

            });
        }



        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseDeveloperExceptionPage();

            app.UseWebSockets();


            //Use the configuration router          

            app.UseWhen(
                    MatchRoutes,
                    ProxyRoute);


            //Route everything else to frontdoor frontend

            if (env.IsDevelopment())
            {
                app.RunProxy(context => context
                    .ForwardTo("https://frontdoor-front.azurewebsites.net")
                     .CopyXForwardedHeaders()
                         .AddXForwardedHeaders()
                         .ApplyCorrelationId()
                    .Send());
            }


        }

        private void ProxyRoute(IApplicationBuilder app)
        {
            app.Use(async (ctx, next) =>
            {
                if (ctx.WebSockets.IsWebSocketRequest)
                {
                    var config = ctx.Features.Get<BaseRoute>();
                    var host = new Uri(config.ProxyPass);

                    await WebSocketHelpers.AcceptProxyWebSocketRequest(ctx, new Uri((ctx.Request.IsHttps ? "wss://" : "ws://") + host.Host + (host.IsDefaultPort ? "" : ":" + host.Port) + ctx.Request.GetEncodedPathAndQuery()));

                }
                else
                {
                    await next();
                }

            });



            app.RunProxy(BuildProxy);
        }

        private async Task<HttpResponseMessage> BuildProxy(HttpContext context)
        {
            var sw = Stopwatch.StartNew();
            var config = context.Features.Get<BaseRoute>(); 

            var forwarded = await config.ForwardAsync(context);
            var timeToBuildForward = sw.Elapsed;
            

            var response = await forwarded
                     .Send();


            // response.Headers.Remove("X-ARR-SSL");
            // response.Headers.Remove("X-AppService-Proto");
            var totalTime = sw.Elapsed;
            var sendtime = totalTime - timeToBuildForward;
            response.Headers.Add("X-ROUTER-TIMINGS", $"{timeToBuildForward}/{sendtime}/{totalTime}");
            return response;
        }

        private bool MatchRoutes(HttpContext arg)
        {
            var findMatch = arg.RequestServices.GetRequiredService<RouteMatcher>().FindMatch(arg);

            if (findMatch != null)
            {
                arg.Features.Set(findMatch);
                return true;
            }

            return false;

        }
    }
}
