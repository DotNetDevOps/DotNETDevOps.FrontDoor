using DotNETDevOps.FrontDoor.AspNetCore;
using DotNETDevOps.FrontDoor.RouterApp.Azure.Blob;
using DotNETDevOps.JsonFunctions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
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
using Newtonsoft.Json.Linq;
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
   public class CorsBuilderContext : ICorsPolicyProvider
    {
        public CorsBuilderContext(ILogger<CorsBuilderContext> logger)
        {
            this.logger = logger;
        }
        public Dictionary<string, CorsPolicy> Builders = new Dictionary<string, CorsPolicy>();
        private readonly ILogger<CorsBuilderContext> logger;

        // public string ActiveBuilderName { get;set; }
        // public CorsPolicyBuilder Active => Builders.ContainsKey(ActiveBuilderName) ? Builders[ActiveBuilderName] : Builders[ActiveBuilderName] = new CorsPolicyBuilder();

        public async Task<CorsPolicy> GetPolicyAsync(HttpContext context, string policyName)
        {
            var config = context.Features.Get<BaseRoute>();
            if (!string.IsNullOrEmpty(config.Cors))
            {
                policyName = config.Cors.ToMD5Hash();
                if (!Builders.ContainsKey(policyName))
                {
                    var ex = new ExpressionParser<CorsPolicyBuilder>(Options.Create(new ExpressionParserOptions<CorsPolicyBuilder>
                    {
                        ThrowOnError = false,
                        Document = new CorsPolicyBuilder()
                    }), logger, new CorsFunctions());

                     await ex.EvaluateAsync(config.Cors);
                    return Builders[policyName] = ex.Document.Build();
                }
                return Builders[policyName];


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
            }
            return null;

        }
    }

    public class CorsFunctions : IExpressionFunctionFactory<CorsPolicyBuilder>
    {
        public ExpressionParser<CorsPolicyBuilder>.ExpressionFunction Get(string name)
        {
            //CorsPolicyBuilder().AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()
            switch (name)
            {
                case "CorsPolicyBuilder":
                    return CorsPolicyBuilder;
                case "AllowAnyOrigin":
                    return AllowAnyOrigin;
                case "AllowAnyMethod":
                    return AllowAnyMethod;
                case "AllowAnyHeader":
                    return AllowAnyHeader;
                case "WithExposedHeaders":
                    return WithExposedHeaders;
                default:
                    throw new NotImplementedException();
            }
        }
        public Task<JToken> CorsPolicyBuilder(CorsPolicyBuilder document,JToken[] args)
        {
             
            return Task.FromResult(JToken.FromObject(new { }));
        }
        public Task<JToken> AllowAnyOrigin(CorsPolicyBuilder document, JToken[] args)
        {
            document.AllowAnyOrigin();
            return Task.FromResult(args.First());
        }
        public Task<JToken> AllowAnyMethod(CorsPolicyBuilder document, JToken[] args)
        {
            document.AllowAnyMethod();
            return Task.FromResult(args.First());
        }
        public Task<JToken> AllowAnyHeader(CorsPolicyBuilder document, JToken[] args)
        {
            document.AllowAnyHeader();
          
            return Task.FromResult(args.First());
        }
        public Task<JToken> WithExposedHeaders(CorsPolicyBuilder document, JToken[] args)
        {
            document.WithExposedHeaders(args.Skip(1).Select(c=>c.ToString()).ToArray());

            return Task.FromResult(args.First());
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
            app.UseCors();

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
