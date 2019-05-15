using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using DotNETDevOps.Extensions.AzureFunctions;
using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using ProxyKit;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System;
using System.Net.WebSockets;
using System.Threading;
using Microsoft.AspNetCore.Http.Extensions;
using System.Collections.Generic;
using DotNETDevOps.FrontDoor.AspNetCore;

namespace DotNETDevOps.FrontDoor.RouterFunction
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

            var routes = JToken.Parse(File.ReadAllText(Path.Combine(this.hostingEnvironment.ContentRootPath, $"routes.{hostingEnvironment.EnvironmentName.ToLower()}.json".Replace(".production", "")))).ToObject<RouteOptions>();

            services.AddSingleton(new RouteMatcher(routes));
        }
     
       

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseDeveloperExceptionPage();

            app.UseWebSockets();

            //app.Use(async (ctx, next) =>
            //{
            //    if (ctx.WebSockets.IsWebSocketRequest)
            //    {
            //        await AcceptProxyWebSocketRequest(ctx,)
            //        //var webSocket = await ctx.WebSockets.AcceptWebSocketAsync();
            //        //await Echo(ctx, webSocket);
            //    }

            //    await next();
            //});

            //Use the configuration router          

            app.UseWhen(
                    MatchRoutes,
                    ProxyRoute);


            //Route everything else to frontdoor frontend

            app.RunProxy(context => context
                .ForwardTo("https://frontdoor-front.azurewebsites.net")
                 .CopyXForwardedHeaders()
                     .AddXForwardedHeaders()
                     .ApplyCorrelationId()
                .Send());



        }

        private void ProxyRoute(IApplicationBuilder app)
        {
            app.Use(async (ctx, next) =>
            {
                if (ctx.WebSockets.IsWebSocketRequest)
                {
                    var config = ctx.Features.Get<BaseRoute>();
                    var host = new Uri(config.Backend);

                    await WebSocketHelpers.AcceptProxyWebSocketRequest(ctx, new Uri((ctx.Request.IsHttps ? "wss://":"ws://")+ host.Host+(host.IsDefaultPort?"": ":"+host.Port) + ctx.Request.GetEncodedPathAndQuery()));
                    //var webSocket = await ctx.WebSockets.AcceptWebSocketAsync();
                    //await Echo(ctx, webSocket);
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
            var config = context.Features.Get<BaseRoute>();



            var forwarded = context
                     .ForwardTo(config.Backend)
                     .CopyXForwardedHeaders()
                     .AddXForwardedHeaders()
                     .ApplyCorrelationId();




            var response = await forwarded
                     .Send();

            if (context.Request.Path.StartsWithSegments("/sockjs-node"))
            {
                Console.WriteLine($"Proxy {response.StatusCode}: {context.Request.GetDisplayUrl()} => {config.Backend}/{context.Request.Path}");
            }
            // response.Headers.Remove("X-ARR-SSL");
            // response.Headers.Remove("X-AppService-Proto");

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
