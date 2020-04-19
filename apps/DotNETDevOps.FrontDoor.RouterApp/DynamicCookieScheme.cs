using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using System;
using System.Threading.Tasks;

namespace DotNETDevOps.FrontDoor.RouterApp
{
    public class DynamicCookieScheme
    {
        

        private readonly IPostConfigureOptions<CookieAuthenticationOptions> postConfigureOptions;
        private readonly IAuthenticationSchemeProvider schemeProvider;
        private readonly IOptionsMonitorCache<CookieAuthenticationOptions> optionsCache;

        public DynamicCookieScheme(IPostConfigureOptions<CookieAuthenticationOptions> postConfigureOptions, IAuthenticationSchemeProvider schemeProvider, IOptionsMonitorCache<CookieAuthenticationOptions> optionsCache)
        {
            this.postConfigureOptions = postConfigureOptions;
            this.schemeProvider = schemeProvider;
            this.optionsCache = optionsCache;
        }

        internal async Task<string> EnsureAddedAsync(string clientId)
        {
            var schemename = $"ProxyAuth-{clientId}";
            
            var scheme = await schemeProvider.GetSchemeAsync(schemename);
            if (scheme == null)
            {
                var o = new CookieAuthenticationOptions();
                o.Cookie.Name = $".auth-proxy-{clientId}";
                o.Cookie.SameSite = SameSiteMode.Strict;
                o.Cookie.Path = "/";
                //  o.Cookie.Domain = "io-board.eu.ngrok.io";
                o.SlidingExpiration = true;
                o.ExpireTimeSpan = TimeSpan.FromDays(30);

                schemeProvider.AddScheme(new AuthenticationScheme(schemename, schemename, typeof(CookieAuthenticationHandler)));
                postConfigureOptions.PostConfigure(schemename, o);
                var added =optionsCache.TryAdd(schemename, o);
            }

            return schemename;
        }

        internal string GetSchema(string clientId)
        {
            var schemename = $"ProxyAuth-{clientId}";
            return schemename;
        }
    }
}