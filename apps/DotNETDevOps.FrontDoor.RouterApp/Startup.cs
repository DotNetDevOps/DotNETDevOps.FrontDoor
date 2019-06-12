using DotNETDevOps.FrontDoor.AspNetCore;
using DotNETDevOps.JsonFunctions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using ProxyKit;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;

namespace DotNETDevOps.FrontDoor.RouterApp
{

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
            services.AddProxy();


            if (!string.IsNullOrEmpty(configuration.GetValue<string>("RemoteConfiguration")))
            {
                services.AddSingleton<IRouteOptionsFactory,RemoteRouteOptionsFactory>();
            }
            else
            {
                services.AddSingleton<IRouteOptionsFactory,FileSystemRouteOptionsFactory>();
            }

          
            services.AddSingleton< RouteMatcher>();

            services.AddHttpClient("ArmClient", http =>
            {

            });
        }



        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseDeveloperExceptionPage();

            app.UseWebSockets();


            app.Map("/.well-known/config.json", b => b.Run(async (r) => {
                await r.Response.WriteAsync(JsonConvert.SerializeObject(r.RequestServices.GetRequiredService<IRouteOptionsFactory>().GetRoutes()));
            }));

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
