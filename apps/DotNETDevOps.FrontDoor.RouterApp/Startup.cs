using DotNETDevOps.FrontDoor.AspNetCore;
using DotNETDevOps.FrontDoor.RouterApp.Blob;
using DotNETDevOps.JsonFunctions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using Microsoft.ReverseProxy.Service.Proxy;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using IHostingEnvironment = Microsoft.AspNetCore.Hosting.IHostingEnvironment;

namespace DotNETDevOps.FrontDoor.RouterApp
{
    public class CustomTransformer : HttpTransformer
    {
        private readonly BaseRoute config;
        private readonly Stopwatch sw;
        private TimeSpan timeToBuildForward;

        public CustomTransformer(BaseRoute config)
        {
            this.config = config;
            sw = Stopwatch.StartNew();
        }
        private void AddHeaders(BaseRoute config, HttpResponseMessage response)
        {
            foreach (var header in config.Headers)
            {
                response.Headers.Add(header.Key, header.Value.AsEnumerable());
            }
          
        }

        public override async Task TransformRequestAsync(HttpContext context, HttpRequestMessage proxyRequest, string destinationPrefix)
        {




            await base.TransformRequestAsync(context, proxyRequest, destinationPrefix);


            await config.ForwardAsync(context,proxyRequest);

            if (config.Authoriztion != null && !string.IsNullOrEmpty(context.Items["forwardtoken"] as string))
            {
                proxyRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", context.Items["forwardtoken"] as string);

            }


            timeToBuildForward = sw.Elapsed;

            //Handle indexes 
            //if (config.Index.Any() && proxyRequest.RequestUri.AbsolutePath.EndsWith("/"))
            //{
            //    var originalUri = proxyRequest.RequestUri;
            //    var queue = new Queue<string>(config.Index);
            //    while (queue.Any())
            //    {
            //        var index = queue.Dequeue();
            //        UriBuilder builder = new UriBuilder(originalUri);
            //        builder.Path += index;

            //        proxyRequest.RequestUri = builder.Uri;


            //       // var response = await forwarded
            //      //      .Send();

            //        if (response.IsSuccessStatusCode)
            //        {
            //            // response.Headers.Remove("X-ARR-SSL");
            //            // response.Headers.Remove("X-AppService-Proto");
            //            var totalTime = sw.Elapsed;
            //            var sendtime = totalTime - timeToBuildForward;
            //            response.Headers.Add("X-ROUTER-TIMINGS", $"{timeToBuildForward}/{sendtime}/{totalTime}");
            //            return AddHeaders(config, response);
            //        }
            //        forwarded = await config.ForwardAsync(context);
            //    }
            //}

            //  forwarded = await config.ForwardAsync(context);
            {

            //    var response = await forwarded
              //           .Send();


              
            }

          
        }
        public override Task TransformResponseAsync(HttpContext context, HttpResponseMessage response)
        {
            // response.Headers.Remove("X-ARR-SSL");
            // response.Headers.Remove("X-AppService-Proto");
            var totalTime = sw.Elapsed;
            var sendtime = totalTime - timeToBuildForward;

            if (context.Request.Headers.ContainsKey("X-GET-BACKEND-ROUTE"))
            {
                context.Response.Headers.Add("X-BACKEND-ROUTE-STATUS-CODE", response.StatusCode.ToString());
                foreach (var h in response.RequestMessage.Headers)
                {
                    try
                    {
                        context.Response.Headers.Add($"X-BACKEND-ROUTE-{h.Key}", string.Join(",", h.Value));
                    }
                    catch (Exception) { }
                }

            }

            if (context.Request.Headers.ContainsKey("X-GET-ROUTER-TIMINGS"))
            {
                response.Headers.Add("X-ROUTER-TIMINGS", $"{timeToBuildForward}/{sendtime}/{totalTime}");

            }



            AddHeaders(config, response);

            return base.TransformResponseAsync(context, response);
        }

        public override Task TransformResponseTrailersAsync(HttpContext httpContext, HttpResponseMessage proxyResponse)
        {
            return base.TransformResponseTrailersAsync(httpContext, proxyResponse);
        }

    }

    
    public class Startup
    {
        private readonly IWebHostEnvironment hostingEnvironment;
        private readonly IConfiguration configuration;

        public Startup(IWebHostEnvironment hostingEnvironment, IConfiguration configuration)
        {
            this.hostingEnvironment = hostingEnvironment;
            this.configuration = configuration;
        }
        public void ConfigureServices(IServiceCollection services)
        {
            
            services.AddSingleton<KeyVaultProvider>();
            services.AddSingleton<VaultTokenCredential>();

    
            services.AddDataProtection(o=>
            {
                
            });

            var httpClient = new HttpMessageInvoker(new SocketsHttpHandler()
            {
                UseProxy = false,
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.None,
                UseCookies = false
            });
             
            services.AddSingleton<HttpMessageInvoker>(httpClient);
            services.AddHttpProxy();

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


            services.AddHealthChecks(); //.AddCheck<HealthCheckRunner>("HealthChecks");

            //using (var sp = services.BuildServiceProvider())
            //{

            //    var ex = new ExpressionParser<CorsBuilderContext>(Options.Create(new ExpressionParserOptions<CorsBuilderContext>
            //    {
            //        ThrowOnError = false,
            //        Document = new CorsBuilderContext()
            //    }), sp.GetService<ILogger<ExpressionParser<ExpressionContext>>>(), new CorsFunctions()); ;

            //    var config = sp.GetRequiredService<IRouteOptionsFactory>();
            //    foreach(var cors in config.GetRoutes().SelectMany(v => v.Value).Where(k => !string.IsNullOrEmpty(k.Cors)))
            //    {
            //        ex.Document.ActiveBuilderName = cors.Cors.ToMD5Hash();
            //        ex.EvaluateAsync(cors.Cors).GetAwaiter().GetResult();
            //    }
            //    services.AddCors(o =>
            //    {
            //        foreach (var c in ex.Document.Builders)
            //            o.AddPolicy(c.Key, c.Value.Build());
            //    });
            //}

            services.AddCors();
            services.AddSingleton<ICorsPolicyProvider, CorsBuilderContext>();


            services.Add(ServiceDescriptor.Singleton<IDistributedCache, AzureTableStorageCacheHandler>(a =>
                new AzureTableStorageCacheHandler(a.GetRequiredService<IConfiguration>().GetValue<string>("AzureWebJobsStorage"), "msal", $"kaapi-{a.GetRequiredService<IHostEnvironment>().EnvironmentName}".ToLower())));
            services.AddSingleton<IMsalTokenCacheProvider, MsalDistributedTokenCacheAdapter>();

            services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<CookieAuthenticationOptions>, PostConfigureCookieAuthenticationOptions>());
            services.AddSingleton<DynamicCookieScheme>();
          
            services.AddAuthentication()
                
        //    services.AddTransient<CookieAuthenticationHandler>();
                .AddCookie(ProxyAuthMiddleware.AuthenticationSchema, o =>
            {
                o.Cookie.Name = ".auth-proxy";
                o.Cookie.SameSite = SameSiteMode.Strict;
             //   o.Cookie.Path = "/";
                //  o.Cookie.Domain = "io-board.eu.ngrok.io";
                o.SlidingExpiration = true;
                o.ExpireTimeSpan = TimeSpan.FromDays(30);
                o.Events.OnSigningIn = CookieSigningIn;

            })
                ;
          //  services.AddScoped<DataPlatformConfidentialClientFactory>();
        }

        private Task CookieSigningIn(CookieSigningInContext arg)
        {
            
            arg.CookieOptions.Path = arg.Properties.Parameters["path"] as string;
            return Task.CompletedTask;
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseHealthChecks("/.well-known/ready", new HealthCheckOptions()
            {
                Predicate = (check) => check.Tags.Contains("ready"),
                ResponseWriter = Writer
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

            app.MapWhen(ctx=>ctx.Request.Path.Value.Contains( "/.auth/"),
                app => app.UseMiddleware<ProxyAuthMiddleware>());

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
                app.Run(async ctx =>
                {
                    var proxy = ctx.RequestServices.GetRequiredService<IHttpProxy>();
                    await proxy.ProxyAsync(ctx, "https://frontdoor-front.azurewebsites.net", ctx.RequestServices.GetRequiredService<HttpMessageInvoker>());

                });
                //app.RunProxy(context => context
                //    .ForwardTo("https://frontdoor-front.azurewebsites.net")
                //     .CopyXForwardedHeaders()
                //         .AddXForwardedHeaders()
                //         .ApplyCorrelationId()
                //    .Send());
            }


        }

     

        private Task Writer(HttpContext httpContext, HealthReport result)
        {
            httpContext.Response.ContentType = "application/json";

            var json = new JObject(
                new JProperty("status", result.Status.ToString()),
                new JProperty("routes", JToken.FromObject( httpContext.RequestServices.GetService< HealthCheckRunner>().Items)),
                new JProperty("results", new JObject(result.Entries.Select(pair =>
                    new JProperty(pair.Key, new JObject(
                        new JProperty("status", pair.Value.Status.ToString()),
                        new JProperty("description", pair.Value.Description),
                        new JProperty("data", new JObject(pair.Value.Data.Select(
                            p => new JProperty(p.Key, p.Value))))))))));
            return httpContext.Response.WriteAsync(
                json.ToString(Formatting.Indented));
        }

        private void ProxyRoute(IApplicationBuilder app)
        {
            
            app.UseCors();

            app.UseMiddleware<ProxyAuthTokenMiddleware>();

            //app.Use(async (ctx, next) =>
            //{
            //    var config = ctx.Features.Get<BaseRoute>();
                

            //    if (ctx.WebSockets.IsWebSocketRequest)
            //    {
                   
            //        var host = new Uri(config.ProxyPass);

            //        await WebSocketHelpers.AcceptProxyWebSocketRequest(ctx, new Uri((ctx.Request.IsHttps ? "wss://" : "ws://") + host.Host + (host.IsDefaultPort ? "" : ":" + host.Port) + ctx.Request.GetEncodedPathAndQuery()));

            //    }
            //    else
            //    {
            //        await next();
            //    }

            //});
            
           


            app.Run(BuildProxy);
        }

        private async Task BuildProxy(HttpContext context)
        {
            var proxy = context.RequestServices.GetRequiredService<IHttpProxy>();
            var http = context.RequestServices.GetRequiredService<HttpMessageInvoker>();


            await proxy.ProxyAsync(context,"https://example.com", http, new RequestProxyOptions { }, new CustomTransformer(context.Features.Get<BaseRoute>()));

         
        }

      

        private bool MatchRoutes(HttpContext context)
        {
            var findMatch = context.RequestServices.GetRequiredService<RouteMatcher>().FindMatch(context);
            
            if (findMatch != null)
            {
                var ex = new ExpressionParser<ExpressionContext>(Options.Create(new ExpressionParserOptions<ExpressionContext>
                {
                    ThrowOnError = false,
                    Document  = new ExpressionContext { BaseRoute = findMatch, HttpContext = context, Upstreams = findMatch.Upstreams }
                }), context.RequestServices.GetService<ILogger<ExpressionParser<ExpressionContext>>>(), findMatch);

                context.Features.Set(ex);
                context.Features.Set(findMatch);



                return true;
            }

            return false;

        }
    }
}
