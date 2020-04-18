using Azure.Core;
using Azure.Security.KeyVault.Secrets;
using DotNETDevOps.FrontDoor.AspNetCore;
using DotNETDevOps.FrontDoor.RouterApp.Azure.Blob;
using DotNETDevOps.JsonFunctions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProxyKit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using IHostingEnvironment = Microsoft.AspNetCore.Hosting.IHostingEnvironment;

namespace DotNETDevOps.FrontDoor.RouterApp
{
    public class VaultTokenCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            var app = new Microsoft.Azure.Services.AppAuthentication.AzureServiceTokenProvider();
            var token = await app.GetAuthenticationResultAsync("https://vault.azure.net");
            return new AccessToken(token.AccessToken, token.ExpiresOn);

        }
    }
    public class KeyVaultProvider 
    {
        private readonly VaultTokenCredential tokenCredential;
        private readonly IConfiguration configuration;

        public KeyVaultProvider(VaultTokenCredential tokenCredential , IConfiguration configuration)
        {
            this.tokenCredential = tokenCredential;
            this.configuration = configuration;
        }
        public async Task<string> GetValueAsync(string name)
        {


            var client = new SecretClient(new Uri(configuration.GetValue<string>("VaultBaseURL")), tokenCredential);
            var secret = await client.GetSecretAsync(name);
            return secret.Value.Value;
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
            //  services.AddTokenManagement();
            services.AddSingleton<KeyVaultProvider>();
            services.AddSingleton<VaultTokenCredential>();

            //services.WithXForwardedHeaders();
            services.AddDataProtection(o=>
            {
                
            });

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
                new AzureTableStorageCacheHandler(a.GetRequiredService<IConfiguration>().GetValue<string>("AzureWebJobsStorage"), "msal", "kaapi")));
            services.AddSingleton<IMsalTokenCacheProvider, MsalDistributedTokenCacheAdapter>();

            services.AddAuthentication().AddCookie("ProxyAuth", o =>
            {
                o.Cookie.Name = ".auth-proxy";
                o.Cookie.SameSite = SameSiteMode.Strict;
                o.Cookie.Path = "/";
              //  o.Cookie.Domain = "io-board.eu.ngrok.io";
                o.SlidingExpiration = true;
                o.ExpireTimeSpan= TimeSpan.FromDays(30);
                
                
            });
          //  services.AddScoped<DataPlatformConfidentialClientFactory>();
        }



        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
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

            app.Map("/.auth", app => app.UseMiddleware<ProxyAuthMiddleware>());

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
            
           


            app.RunProxy(BuildProxy);
        }

        private async Task<HttpResponseMessage> BuildProxy(HttpContext context)
        {
           
            var config = context.Features.Get<BaseRoute>();
           

          

            var sw = Stopwatch.StartNew();
            var forwarded = await config.ForwardAsync(context);

            if (config.Authoriztion != null && !string.IsNullOrEmpty(context.Items["forwardtoken"] as string))
            {
                forwarded.UpstreamRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", context.Items["forwardtoken"] as string);
                
            }


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
                        return AddHeaders(config, response);
                    }
                    forwarded = await config.ForwardAsync(context);
                }
            }

          //  forwarded = await config.ForwardAsync(context);
            {

                var response = await forwarded
                         .Send();


                // response.Headers.Remove("X-ARR-SSL");
                // response.Headers.Remove("X-AppService-Proto");
                var totalTime = sw.Elapsed;
                var sendtime = totalTime - timeToBuildForward;
                
                if (context.Request.Headers.ContainsKey("X-GET-BACKEND-ROUTE"))
                {
                    context.Response.Headers.Add("X-BACKEND-ROUTE-STATUS-CODE", response.StatusCode.ToString());
                    foreach(var h in response.RequestMessage.Headers)
                    {
                        try
                        {
                            context.Response.Headers.Add($"X-BACKEND-ROUTE-{h.Key}",string.Join("," , h.Value));
                        }catch(Exception) { }
                    }
                  
                }

                if (context.Request.Headers.ContainsKey("X-GET-ROUTER-TIMINGS"))
                {
                    response.Headers.Add("X-ROUTER-TIMINGS", $"{timeToBuildForward}/{sendtime}/{totalTime}");

                }

                 
               
                return AddHeaders(config, response);
            }
        }

        private HttpResponseMessage AddHeaders(BaseRoute config, HttpResponseMessage response)
        {
            foreach(var header in config.Headers)
            {
                response.Headers.Add(header.Key, header.Value.AsEnumerable());
            }
            return response;
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
