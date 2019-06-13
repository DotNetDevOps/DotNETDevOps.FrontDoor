using DotNETDevOps.FrontDoor.AspNetCore;
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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using ProxyKit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IHostingEnvironment = Microsoft.AspNetCore.Hosting.IHostingEnvironment;

namespace DotNETDevOps.FrontDoor.RouterApp
{
    public class HeathCheckItem :IComparable<HeathCheckItem>
    {
        public DateTimeOffset NextRun { get; set; }
        public string Url { get;  set; }
        public int Interval { get;  set; }
        public int Failed { get;  set; }

        public int CompareTo(HeathCheckItem other)
        {
            return this.NextRun.CompareTo(other.NextRun);
        }
    }
    public class HealthCheckRunner : BackgroundService
    {
        private List<HeathCheckItem> data;
        private readonly IHttpClientFactory httpClientFactory;
        private readonly ILogger<HealthCheckRunner> logger;

        public HealthCheckRunner(IHttpClientFactory httpClientFactory, ILogger<HealthCheckRunner> logger)
        {
            data = new List<HeathCheckItem>();
            this.httpClientFactory = httpClientFactory;
            this.logger = logger;
        }

        public void Enqueue(HeathCheckItem item)
        {
            data.Add(item);
            int ci = data.Count - 1; // child index; start at end
            while (ci > 0)
            {
                int pi = (ci - 1) / 2; // parent index
                if (data[ci].CompareTo(data[pi]) >= 0)
                    break; // child item is larger than (or equal) parent so we're done
                HeathCheckItem tmp = data[ci];
                data[ci] = data[pi];
                data[pi] = tmp;
                ci = pi;
            }
        }

        public HeathCheckItem Dequeue()
        {
            // assumes pq is not empty; up to calling code
            int li = data.Count - 1; // last index (before removal)
            HeathCheckItem frontItem = data[0];   // fetch the front
            data[0] = data[li];
            data.RemoveAt(li);

            --li; // last index (after removal)
            int pi = 0; // parent index. start at front of pq
            while (true)
            {
                int ci = pi * 2 + 1; // left child index of parent
                if (ci > li)
                    break;  // no children so done
                int rc = ci + 1;     // right child
                if (rc <= li && data[rc].CompareTo(data[ci]) < 0) // if there is a rc (ci + 1), and it is smaller than left child, use the rc instead
                    ci = rc;
                if (data[pi].CompareTo(data[ci]) <= 0)
                    break; // parent is smaller than (or equal to) smallest child so done
                HeathCheckItem tmp = data[pi];
                data[pi] = data[ci];
                data[ci] = tmp; // swap parent and child
                pi = ci;
            }
            return frontItem;
        }

        public HeathCheckItem Peek()
        {
            HeathCheckItem frontItem = data[0];
            return frontItem;
        }

        public void AddHealthCheckTask(HeathCheckItem item)
        {
            this.Enqueue(item);
        }
        private readonly SemaphoreSlim _syncLock = new SemaphoreSlim(initialCount: 1);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            while (!stoppingToken.IsCancellationRequested)
            {

                if(data.Count > 0)
                {

                    await _syncLock.WaitAsync().ConfigureAwait(false);

                    var item = Dequeue();
                    try
                    {
                        var delay = item.NextRun - DateTimeOffset.UtcNow;
                        if (delay > TimeSpan.Zero)
                            await Task.Delay(delay, stoppingToken);

                        if (!stoppingToken.IsCancellationRequested)
                        {

                            using (var http = httpClientFactory.CreateClient("heathcheck"))
                            {
                                try
                                {
                                    var resp = await http.SendAsync(new HttpRequestMessage(HttpMethod.Get, item.Url));

                                    if (!resp.IsSuccessStatusCode)
                                    {
                                        logger.LogWarning("{url} is not live: {status}", item.Url, resp.StatusCode);
                                        item.Failed++;
                                    }
                                    else
                                    {
                                        item.Failed = 0;
                                    }

                                }
                                catch(Exception ex)
                                {
                                    logger.LogError("{url} is not live", item.Url);
                                }

                            }

                        }
                    }
                    finally
                    {
                        item.NextRun = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(item.Interval);
                        Enqueue(item);
                        _syncLock.Release();
                    }
                }


                if (data.Count == 0)
                    await Task.Delay(5000, stoppingToken);



            }
        }

        internal async Task ClearAsync()
        {
            await _syncLock.WaitAsync().ConfigureAwait(false);
            try
            {
                this.data.Clear();

            }
            finally
            {
                _syncLock.Release();
            }
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
            services.AddProxy();
            services.AddHttpClient("heathcheck");
            services.AddSingleton<HealthCheckRunner>();
              services.AddSingleton<IHostedService, HealthCheckRunner>(sp => sp.GetRequiredService<HealthCheckRunner>());




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
