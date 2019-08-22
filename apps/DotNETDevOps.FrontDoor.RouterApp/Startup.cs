using DotNETDevOps.FrontDoor.AspNetCore;
using DotNETDevOps.FrontDoor.RouterApp.Azure.Blob;
using DotNETDevOps.JsonFunctions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using ProxyKit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using IHostingEnvironment = Microsoft.AspNetCore.Hosting.IHostingEnvironment;

namespace DotNETDevOps.FrontDoor.RouterApp
{

    public static class myex
    {
        public static ForwardContext CopyXForwardedHeaders(this ForwardContext forwardContext)
        {
            var headers = forwardContext.UpstreamRequest.Headers;

            if (forwardContext.HttpContext.Request.Headers.TryGetValue(XForwardedExtensions.XForwardedFor, out var forValues))
            {
                headers.Remove(XForwardedExtensions.XForwardedFor);
                headers.TryAddWithoutValidation(XForwardedExtensions.XForwardedFor, forValues.ToArray());
            }

            if (forwardContext.HttpContext.Request.Headers.TryGetValue(XForwardedExtensions.XForwardedHost, out var hostValues))
            {
                headers.Remove(XForwardedExtensions.XForwardedHost);
                headers.TryAddWithoutValidation(XForwardedExtensions.XForwardedHost, hostValues.ToArray());
            }

            if (forwardContext.HttpContext.Request.Headers.TryGetValue(XForwardedExtensions.XForwardedProto, out var protoValues))
            {
               // var a = protoValues.SelectMany(k => k.Split(',').Select(t => t.Trim())).ToArray();
                headers.Remove(XForwardedExtensions.XForwardedProto);
                headers.TryAddWithoutValidation(XForwardedExtensions.XForwardedProto, protoValues.SelectMany(k=>k.Split(',').Select(t=>t.Trim())).Distinct().ToArray());
            }

            if (forwardContext.HttpContext.Request.Headers.TryGetValue(XForwardedExtensions.XForwardedPathBase, out var pathBaseValues))
            {
                headers.Remove(XForwardedExtensions.XForwardedPathBase);
                headers.TryAddWithoutValidation(XForwardedExtensions.XForwardedPathBase, pathBaseValues.ToArray());
            }

            return forwardContext;
        }
    }

    public class Startup
    {
        private readonly IHostingEnvironment hostingEnvironment;
        private readonly IConfiguration configuration;

        public Startup(IHostingEnvironment hostingEnvironment, IConfiguration configuration)
        {
            this.hostingEnvironment = hostingEnvironment;
            this.configuration = configuration;
        }
        public void ConfigureServices(IServiceCollection services)
        {
           // services.WithXForwardedHeaders();

            services.AddProxy();
            services.AddHttpClient("heathcheck");
            services.AddSingleton<HealthCheckRunner>();
              services.AddSingleton<IHostedService, HealthCheckRunner>(sp => sp.GetRequiredService<HealthCheckRunner>());

            services.AddSingleton<CDNHelperFactory>();



            if (!string.IsNullOrEmpty(configuration.GetValue<string>("RemoteConfiguration")))
            {
                services.AddSingleton<IRouteOptionsFactory,RemoteRouteOptionsFactory>();
            }
            else
            {
                services.AddSingleton<IRouteOptionsFactory,FileSystemRouteOptionsFactory>();
            }
            services.AddSingleton<IHostedService, IRouteOptionsFactory>(sp => sp.GetRequiredService<IRouteOptionsFactory>());
          
            services.AddSingleton< RouteMatcher>();

            services.AddHttpClient("ArmClient", http =>
            {

            });


            services.AddHealthChecks();
           
        }



        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseHealthChecks("/.well-known/ready", new HealthCheckOptions()
            {
                Predicate = (check) => check.Tags.Contains("ready"),
            });

            app.UseHealthChecks("/.well-known/live",new HealthCheckOptions
            {
                 Predicate = (_)=>false
            });

            app.UseDeveloperExceptionPage();

            app.UseWebSockets();


            app.Map("/.well-known/config.json", b => b.Run(async (r) => {
                await r.Response.WriteAsync(JsonConvert.SerializeObject(r.RequestServices.GetRequiredService<IRouteOptionsFactory>().GetRoutes()));
            }));

            //app.Use(async (ctx, next) =>
            //{
            //    var sb = new StringBuilder();
            //    sb.AppendLine($"{ctx.Request.Method} {ctx.Request.GetDisplayUrl()}");
            //    foreach (var h in ctx.Request.Headers)
            //    {
            //        sb.AppendLine($" {h.Key} {string.Join(",", h.Value)}");
            //    }

            //    var str = sb.ToString();
            //    Console.WriteLine(str);
            //    await next();
            //});
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
            
            var config = context.Features.Get<BaseRoute>();
            var sw = Stopwatch.StartNew();
            var forwarded = await config.ForwardAsync(context);
            var timeToBuildForward = sw.Elapsed;

            //Handle indexes 
            if (config.Index.Any() && forwarded.UpstreamRequest.RequestUri.AbsolutePath.EndsWith("/"))
            {
                var originalUri = forwarded.UpstreamRequest.RequestUri;
                var queue = new Queue<string>(config.Index);
                while (queue.Any())
                {
                    var index = queue.Dequeue();
                    UriBuilder builder = new UriBuilder(originalUri);
                    builder.Path += index;
                    forwarded.UpstreamRequest.RequestUri = builder.Uri;

                    var response = await forwarded
                        .Send();

                    if (response.IsSuccessStatusCode)
                    {
                        // response.Headers.Remove("X-ARR-SSL");
                        // response.Headers.Remove("X-AppService-Proto");
                        var totalTime = sw.Elapsed;
                        var sendtime = totalTime - timeToBuildForward;
                        response.Headers.Add("X-ROUTER-TIMINGS", $"{timeToBuildForward}/{sendtime}/{totalTime}");
                        return response;
                    }
                }
            }

            {

                var response = await forwarded
                         .Send();


                // response.Headers.Remove("X-ARR-SSL");
                // response.Headers.Remove("X-AppService-Proto");
                var totalTime = sw.Elapsed;
                var sendtime = totalTime - timeToBuildForward;
                response.Headers.Add("X-ROUTER-TIMINGS", $"{timeToBuildForward}/{sendtime}/{totalTime}");
                return response;
            }
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
