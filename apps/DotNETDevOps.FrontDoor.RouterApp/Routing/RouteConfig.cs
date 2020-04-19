using DotNETDevOps.FrontDoor.RouterApp.Azure.Blob;
using DotNETDevOps.JsonFunctions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.Identity.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProxyKit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DotNETDevOps.FrontDoor.RouterApp
{
    public class ExpressionContext
    {
        public BaseRoute BaseRoute { get;  set; }
        public HttpContext HttpContext { get;  set; }
        public Dictionary<string, Upstream> Upstreams { get;  set; }

        public UpstreamHostServer Upstream { get; set; }
        public IServiceProvider Services => HttpContext.RequestServices;
    }
    public class HealthCheck
    {
        public int? Interval { get; set; }
        public int? Delay { get; set; }
        public PathString Path { get; set; } = "/";

        private HeathCheckItem _heathCheckItem;

        public HeathCheckItem GetHeathCheckItem(BaseRoute location)
        {
            if (_heathCheckItem != null)
            {
                return _heathCheckItem;
            }

            ExpressionContext expressionContext = null;

            var proxyUrl = location.ProxyPass;
            //if (proxyUrl.StartsWith("["))
            //{
            //    var ex = new ExpressionParser<ExpressionContext>(Options.Create(new ExpressionParserOptions<ExpressionContext>
            //    {
            //        ThrowOnError = false,
            //        Document = expressionContext = new ExpressionContext { BaseRoute = this, HttpContext = context, Upstreams = this.upstreams }
            //    }), context.RequestServices.GetService<ILogger<ExpressionParser<ExpressionContext>>>(), this);

            //    proxyUrl = (await ex.EvaluateAsync(proxyUrl)).ToString();
            //}

            var url = new Uri(proxyUrl);
          

            if (location.Upstreams.ContainsKey(url.Host))
            {
                var upstream = expressionContext?.Upstream ?? location.Upstreams[url.Host].GetUpstreamHost();

                proxyUrl = proxyUrl.Replace(url.Host, upstream.Host);

            }

            return _heathCheckItem = new HeathCheckItem
                {
                    Interval = Interval ?? 5,
                    NextRun = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(Delay ?? 0),
                    Url = new Uri(proxyUrl).GetLeftPart(UriPartial.Authority) + Path
            };

            

        }
    }
    public class RouteAuthorization
    {
        //[JsonProperty("clientid")]
        //public string ClientId { get; set; }

        [JsonProperty("scopes")]
        public string[] Scopes { get; set; }

       // private readonly Lazy<IConfidentialClientApplication> _app;
        public RouteAuthorization()
        {
            //_app = new Lazy<IConfidentialClientApplication>(() =>
            //{
            //    var appBuilder = ConfidentialClientApplicationBuilder.Create(ClientId)
            //   .WithTenantId("common")

            //   .WithRedirectUri("https://io-board.eu.ngrok.io/.auth/login/aad/callback");
            //    if (!string.IsNullOrEmpty(ClientSecret))
            //        appBuilder.WithClientSecret(ClientSecret);



            //    return appBuilder.Build();
            //});
        }

      //  public IConfidentialClientApplication App => _app.Value;
      public static async Task<IConfidentialClientApplication> BuildAppAsync(string host,string clientId, IMsalTokenCacheProvider msalTokenCacheProvider, KeyVaultProvider keyVaultProvider)
        {
            var appBuilder = ConfidentialClientApplicationBuilder.Create(clientId)
             .WithTenantId("common") 
             .WithRedirectUri($"https://{host}/.auth/login/aad/callback");

            if(keyVaultProvider!=null)
                appBuilder.WithClientSecret(await keyVaultProvider.GetValueAsync(clientId));
             

            var app= appBuilder.Build();
            msalTokenCacheProvider?.Initialize(app.UserTokenCache);
            return app;
        }

        public static async Task<string> GetRedirectUrl(string displayUrl,string clientid, string redirectUrl, string[] scopes)
        {

            var host = new Uri(displayUrl);

            
            var app = await BuildAppAsync(host.Host,clientid,null,null); 
            var location=await    app.GetAuthorizationRequestUrl(scopes)
                .WithExtraQueryParameters($"state={Base64Url.Encode(Encoding.ASCII.GetBytes($"path={host.AbsolutePath.Substring(0,host.AbsolutePath.IndexOf(".auth/"))}&redirectUrl={redirectUrl}&clientid={app.AppConfig.ClientId}"))}").ExecuteAsync();
            return location.AbsoluteUri;
            
            
        }
    }
    [JsonConverter(typeof(RouteConfigConverter))]
    public abstract class BaseRoute : IExpressionFunctionFactory<ExpressionContext>
    {
        public abstract int Precedence { get; }
        public int RelativePrecedence { get; protected set; } = 0;
        public string Route { get; set; }

        [JsonProperty("authorization")]
        public RouteAuthorization Authoriztion { get; set; }

        [JsonProperty("proxy_pass")]
        public string ProxyPass { get; set; }

        [JsonProperty("cors")]
        public string Cors { get; set; }

        [JsonProperty("index")]
        public string[] Index { get; set; } = Array.Empty<string>();

        [JsonProperty("proxy_set_header")]
        public Dictionary<string, string> ProxySetHeader { get; set; } = new Dictionary<string, string>();
      
        [JsonProperty("headers")]
        public Dictionary<string, StringValues> Headers { get; set; } = new Dictionary<string, StringValues>();

        [JsonProperty("rewrite")]
        public string Rewrite { get; set; }

        [JsonProperty("health_check")]
        public HealthCheck HealthCheck { get; set; }

        // public string[] Hostnames { get; set; } = new string[0];

        public abstract bool IsMatch(string url);

        public bool StopOnMatch { get; protected set; }

        public abstract void Initialize();

        private Server server;
       
        public Dictionary<string, Upstream> Upstreams { get; private set; }

        internal void SetServer(System.Collections.Generic.Dictionary<string, Upstream> upstreams, Server server)
        {
            if (upstreams == null)
            {
                throw new ArgumentNullException(nameof(upstreams));
            }

            if (server == null)
            {
                throw new ArgumentNullException(nameof(server));
            }
            this.server = server;
            this.Upstreams = upstreams;
        }
        public virtual void RewriteUrl(HttpContext context)
        {
            if (!string.IsNullOrEmpty(Rewrite))
            {
                var url = new Uri(context.Request.GetDisplayUrl());
                var path = url.AbsolutePath;
                if (Regex.IsMatch(path, Rewrite.Split(' ').First()))
                {
                    var newUrl= Regex.Replace(path, Rewrite.Split(' ').First(), Rewrite.Split(' ').Skip(1).First().Replace("$request_uri", url.AbsoluteUri));
                    if (newUrl.StartsWith("/"))
                    {
                        context.Request.Path = newUrl;
                    }
                    else
                    {
                        var newUri = new Uri(newUrl);

                        context.Request.Scheme = newUri.Scheme;
                        context.Request.Host = new HostString(newUri.Host);
                        context.Request.Path = newUri.AbsolutePath;
                        context.Request.QueryString = new QueryString(newUri.Query);
                    }


                }
            }

           
        }
        public async Task<ForwardContext> ForwardAsync(HttpContext context)
        {
            // ExpressionContext expressionContext = null;
            var ex = context.Features.Get<ExpressionParser<ExpressionContext>>();
            var proxyUrl = ProxyPass;

            if (proxyUrl.StartsWith("["))
            {
                //var ex = new ExpressionParser<ExpressionContext>(Options.Create(new ExpressionParserOptions<ExpressionContext> {
                //    ThrowOnError = false, Document = expressionContext= new ExpressionContext { BaseRoute = this, HttpContext = context, Upstreams=this.Upstreams  } }), context.RequestServices.GetService<ILogger< ExpressionParser<ExpressionContext>>>(), this);

                proxyUrl = (await ex.EvaluateAsync(proxyUrl)).ToString();
            }

            var url = new Uri(proxyUrl);
            
            if (Upstreams.ContainsKey(url.Host))
            {
                var upstream = ex.Document?.Upstream ?? Upstreams[url.Host].GetUpstreamHost();

                proxyUrl = proxyUrl.Replace(url.Host, upstream.Host);

            }

            RewriteUrl(context);

            
            
          
            var forwarded = context
                    .ForwardTo(proxyUrl)
                   // .CopyXForwardedHeaders()
                    .AddXForwardedHeaders()
                    .ApplyCorrelationId();

            foreach (var h in ProxySetHeader)
            {
                forwarded.UpstreamRequest.Headers.Add(h.Key, h.Value);
            }


            if (Upstreams.ContainsKey(url.Host))
            {
                var upstream = Upstreams[url.Host].GetUpstreamHost(); 
                var azurefunction = upstream?.Extensions?.SelectToken("$.azure.functions.authentication");
                if (azurefunction != null)
                {
                    var armUrl = $"https://management.azure.com/subscriptions/{azurefunction.SelectToken("$.subscriptionId")}/resourceGroups/{azurefunction.SelectToken("$.resourceGroupName")}/providers/Microsoft.Web/sites/{azurefunction.SelectToken("$.name")}{(azurefunction.SelectToken("$.slot") != null ? $"/slots/{azurefunction.SelectToken("$.slot")}" : "")}/functions/admin/token?api-version=2018-02-01";
                    var http = context.RequestServices.GetService<IHttpClientFactory>().CreateClient("ArmClient");

                    var tokenservice = new AzureServiceTokenProvider();
                    var token = await tokenservice.GetAccessTokenAsync("https://management.azure.com/");

                    var req = new HttpRequestMessage(HttpMethod.Get, armUrl);
                    req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                    var functionTokenRsp = await http.SendAsync(req);
                    var functionToken = await functionTokenRsp.Content.ReadAsStringAsync();
                    forwarded.UpstreamRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", functionToken.Trim('"'));

                }

            }

            if (context.Request.Headers.ContainsKey("X-GET-BACKEND-ROUTE"))
            {
                context.Response.Headers.Add("X-BACKEND-ROUTE-HOST", proxyUrl);
                context.Response.Headers.Add("X-BACKEND-ROUTE-URI", forwarded.UpstreamRequest.RequestUri.ToString());
            }

            return forwarded;
        }

        public ExpressionParser<ExpressionContext>.ExpressionFunction Get(string name)
        {

            switch (name)
            {
                case "blobFindVersion":
                    return blobFindVersion;
                case "concat":
                    return concat;
            }

            return null;
        }

        private Task<JToken> concat(ExpressionParser<ExpressionContext> parser,ExpressionContext document, JToken[] arguments)
        {
            return Task.FromResult((JToken)string.Join("", arguments.Select(v=>v.ToString())));
        }

        private static async Task<JToken> blobFindVersion(ExpressionParser<ExpressionContext> parser, ExpressionContext context, JToken[] arguments)
        {
            var proxyUrl = arguments[0].ToString();
          
            var url = new Uri(proxyUrl);

            var upstream = context.Upstream = context.Upstreams[url.Host].GetUpstreamHost();

            proxyUrl = proxyUrl.Replace(url.Host, upstream.Host);

            var cacheHelperFactory = context.HttpContext.RequestServices.GetRequiredService<CDNHelperFactory>();

            var cdn = cacheHelperFactory.CreateCDNHelper(proxyUrl, arguments[1].ToString()); // new CDNHelper(proxyUrl, arguments[1].ToString());
            var version =await cdn.GetAsync("*", arguments.Skip(2).FirstOrDefault()?.ToString());

            //  var configuration = document.HttpContext.RequestServices.GetRequiredService<IConfiguration>();

            context.HttpContext.Response.Headers["x-blobfind-version"] = version.Version;

            return proxyUrl + arguments[1].ToString() + "/" + version.Version +"/";
        }
    }
}
