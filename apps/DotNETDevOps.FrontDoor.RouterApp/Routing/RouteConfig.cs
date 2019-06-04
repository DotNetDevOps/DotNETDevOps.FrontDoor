using DotNETDevOps.FrontDoor.RouterApp.Azure.Blob;
using DotNETDevOps.JsonFunctions;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
using System.Threading.Tasks;

namespace DotNETDevOps.FrontDoor.RouterApp
{
    public class ExpressionContext
    {
        public BaseRoute BaseRoute { get;  set; }
        public HttpContext HttpContext { get;  set; }
        public Dictionary<string, Upstream> Upstreams { get;  set; }

        public UpstreamHostServer Upstream { get; set; }
    }
    [JsonConverter(typeof(RouteConfigConverter))]
    public abstract class BaseRoute : IExpressionFunctionFactory<ExpressionContext>
    {
        public abstract int Precedence { get; }
        public int RelativePrecedence { get; protected set; } = 0;
        public string Route { get; set; }

        [JsonProperty("proxy_pass")]
        public string ProxyPass { get; set; }

        [JsonProperty("proxy_set_header")]
        public Dictionary<string, string> ProxySetHeader { get; set; } = new Dictionary<string, string>();

        // public string[] Hostnames { get; set; } = new string[0];

        public abstract bool IsMatch(string url);

        public bool StopOnMatch { get; protected set; }

        public abstract void Initialize();

        private Server server;
        private Dictionary<string, Upstream> upstreams;
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
            this.upstreams = upstreams;
        }
        public virtual void RewriteUrl(HttpContext context)
        {

        }
        public async Task<ForwardContext> ForwardAsync(HttpContext context)
        {
            ExpressionContext expressionContext = null;

              var proxyUrl = ProxyPass;
            if (proxyUrl.StartsWith("["))
            {
                var ex = new ExpressionParser<ExpressionContext>(Options.Create(new ExpressionParserOptions<ExpressionContext> {
                    ThrowOnError = false, Document = expressionContext= new ExpressionContext { BaseRoute = this, HttpContext = context, Upstreams=this.upstreams  } }), context.RequestServices.GetService<ILogger< ExpressionParser<ExpressionContext>>>(), this);

                proxyUrl = (await ex.EvaluateAsync(proxyUrl)).ToString();
            }

            var url = new Uri(proxyUrl);
            
            if (upstreams.ContainsKey(url.Host))
            {
                var upstream = expressionContext?.Upstream ?? upstreams[url.Host].GetUpstreamHost();

                proxyUrl = proxyUrl.Replace(url.Host, upstream.Host);

            }

            RewriteUrl(context);

            
          
            var forwarded = context
                    .ForwardTo(proxyUrl)
                    .CopyXForwardedHeaders()
                    .AddXForwardedHeaders()
                    .ApplyCorrelationId();

            foreach (var h in ProxySetHeader)
            {
                forwarded.UpstreamRequest.Headers.Add(h.Key, h.Value);
            }


            if (upstreams.ContainsKey(url.Host))
            {
                var upstream = upstreams[url.Host].GetUpstreamHost(); 
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

        private Task<JToken> concat(ExpressionContext document, JToken[] arguments)
        {
            return Task.FromResult((JToken)string.Join("", arguments.Select(v=>v.ToString())));
        }

        private static async Task<JToken> blobFindVersion(ExpressionContext document, JToken[] arguments)
        {
            var proxyUrl = arguments[0].ToString();
            var url = new Uri(proxyUrl);

            var upstream = document.Upstream = document.Upstreams[url.Host].GetUpstreamHost();

            proxyUrl = proxyUrl.Replace(url.Host, upstream.Host);


            var cdn = new CDNHelper(proxyUrl,arguments[1].ToString());
            var version =await cdn.GetAsync();

            //  var configuration = document.HttpContext.RequestServices.GetRequiredService<IConfiguration>();


            return proxyUrl + arguments[1].ToString() + "/" + version.Version +"/";
        }
    }
}
