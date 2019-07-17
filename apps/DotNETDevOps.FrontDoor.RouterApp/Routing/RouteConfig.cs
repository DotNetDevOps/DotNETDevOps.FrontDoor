﻿using DotNETDevOps.FrontDoor.RouterApp.Azure.Blob;
using DotNETDevOps.JsonFunctions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
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
    [JsonConverter(typeof(RouteConfigConverter))]
    public abstract class BaseRoute : IExpressionFunctionFactory<ExpressionContext>
    {
        public abstract int Precedence { get; }
        public int RelativePrecedence { get; protected set; } = 0;
        public string Route { get; set; }

        [JsonProperty("proxy_pass")]
        public string ProxyPass { get; set; }

        [JsonProperty("index")]
        public string[] Index { get; set; } = Array.Empty<string>();

        [JsonProperty("proxy_set_header")]
        public Dictionary<string, string> ProxySetHeader { get; set; } = new Dictionary<string, string>();

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
                var url = context.Request.GetDisplayUrl();

                if (Regex.IsMatch(url, Rewrite.Split(' ').First()))
                {
                    var newUrl= Regex.Replace(url, Rewrite.Split(' ').First(), Rewrite.Split(' ').Last().Replace("$request_uri", url));
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
            ExpressionContext expressionContext = null;

              var proxyUrl = ProxyPass;
            if (proxyUrl.StartsWith("["))
            {
                var ex = new ExpressionParser<ExpressionContext>(Options.Create(new ExpressionParserOptions<ExpressionContext> {
                    ThrowOnError = false, Document = expressionContext= new ExpressionContext { BaseRoute = this, HttpContext = context, Upstreams=this.Upstreams  } }), context.RequestServices.GetService<ILogger< ExpressionParser<ExpressionContext>>>(), this);

                proxyUrl = (await ex.EvaluateAsync(proxyUrl)).ToString();
            }

            var url = new Uri(proxyUrl);
            
            if (Upstreams.ContainsKey(url.Host))
            {
                var upstream = expressionContext?.Upstream ?? Upstreams[url.Host].GetUpstreamHost();

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


            var cdn = new CDNHelper(proxyUrl, arguments[1].ToString());
            var version =await cdn.GetAsync("*", arguments.Skip(2).FirstOrDefault()?.ToString());

            //  var configuration = document.HttpContext.RequestServices.GetRequiredService<IConfiguration>();


            return proxyUrl + arguments[1].ToString() + "/" + version.Version +"/";
        }
    }
}
