using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Identity.Client;
using System;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace DotNETDevOps.FrontDoor.RouterApp
{
    /// <summary>
    /// Base64Url encoder/decoder
    /// </summary>
    public static class Base64Url
    {
        /// <summary>
        /// Encodes the specified byte array.
        /// </summary>
        /// <param name="arg">The argument.</param>
        /// <returns></returns>
        public static string Encode(byte[] arg)
        {
            var s = Convert.ToBase64String(arg); // Standard base64 encoder

            s = s.TrimEnd('=');// Remove any trailing '='s
            s = s.Replace('+', '-'); // 62nd char of encoding
            s = s.Replace('/', '_'); // 63rd char of encoding

            return s;
        }

        /// <summary>
        /// Decodes the specified string.
        /// </summary>
        /// <param name="arg">The argument.</param>
        /// <returns></returns>
        /// <exception cref="System.Exception">Illegal base64url string!</exception>
        public static byte[] Decode(string arg)
        {
            var s = arg;
            s = s.Replace('-', '+'); // 62nd char of encoding
            s = s.Replace('_', '/'); // 63rd char of encoding

            switch (s.Length % 4) // Pad with trailing '='s
            {
                case 0: break; // No pad chars in this case
                case 2: s += "=="; break; // Two pad chars
                case 3: s += "="; break; // One pad char
                default: throw new Exception("Illegal base64url string!");
            }

            return Convert.FromBase64String(s); // Standard base64 decoder
        }
    }
    public class ProxyAuthTokenMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly KeyVaultProvider keyVaultProvider;
        private readonly IMsalTokenCacheProvider msalTokenCacheProvider;

        public ProxyAuthTokenMiddleware(RequestDelegate next, KeyVaultProvider keyVaultProvider, IMsalTokenCacheProvider msalTokenCacheProvider)
        {
            _next = next;
            this.keyVaultProvider = keyVaultProvider;
            this.msalTokenCacheProvider = msalTokenCacheProvider;
        }

        public async Task InvokeAsync(HttpContext ctx)
        {
            var config = ctx.Features.Get<BaseRoute>();
            if (config.Authoriztion != null && !ctx.Request.Headers.ContainsKey("Authorization"))
            {
                AuthenticationResult result = null;

                var auth = await ctx.AuthenticateAsync("ProxyAuth");
                if (!auth.Succeeded)
                {
                    ctx.Response.StatusCode = 401;
                    return;
                }
                ;

                var app = config.Authoriztion.BuildApp(new Uri(ctx.Request.GetDisplayUrl()).Host,msalTokenCacheProvider);



                var account = MsalAccount.FromMsalAccountId(auth.Principal.FindFirstValue("sub"));
                // MsalAccount

                try
                {

                    //var accounts = await app.GetAccountsAsync();

                    result = await app.AcquireTokenSilent(config.Authoriztion.Scopes, account)
                           .ExecuteAsync();
                    ctx.Items["forwardtoken"] = result.AccessToken;

                }
                catch (MsalUiRequiredException ex)
                {

                    ctx.Response.StatusCode = 401;

                    return;
                }
                catch (Exception ex)
                {

                    // await ctx.SignOutAsync("ProxyAuth");
                    ctx.Response.StatusCode = 401;

                    return;
                }



            }


            await _next(ctx);
        }
        
    }
    public class ProxyAuthMiddleware
    {
        const string AuthenticationSchema = "ProxyAuth";
        private readonly RequestDelegate _next;
        private readonly KeyVaultProvider keyVaultProvider;
        private readonly IMsalTokenCacheProvider msalTokenCacheProvider;

        public ProxyAuthMiddleware(RequestDelegate next, KeyVaultProvider keyVaultProvider, IMsalTokenCacheProvider msalTokenCacheProvider)
        {
            _next = next;
            this.keyVaultProvider = keyVaultProvider;
            this.msalTokenCacheProvider = msalTokenCacheProvider;
        }

        public async Task InvokeAsync(HttpContext ctx)
        {
            if (ctx.Request.Path.Value.EndsWith("callback"))
            {
                var data = HttpUtility.ParseQueryString(Encoding.ASCII.GetString(Base64Url.Decode(ctx.Request.Query["state"].FirstOrDefault())));
                var secret = await keyVaultProvider.GetValueAsync(data["clientid"]);

                var auth = new RouteAuthorization()
                {
                    ClientId = data["clientid"],
                    ClientSecret = secret,
                };
                var app = auth.BuildApp(new Uri(ctx.Request.GetDisplayUrl()).Host,msalTokenCacheProvider);
                 
                var token = await app.AcquireTokenByAuthorizationCode(new string[] { "profile" }, ctx.Request.Query["code"].FirstOrDefault()).ExecuteAsync();

                await ctx.SignInAsync(AuthenticationSchema, new ClaimsPrincipal(
                    new ClaimsIdentity(new Claim[]{
                            new Claim("sub",$"{token.Account.HomeAccountId.ObjectId}.{token.Account.HomeAccountId.TenantId}")
                        }, AuthenticationSchema)), new AuthenticationProperties { IsPersistent = true, AllowRefresh = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14) });

                ctx.Response.Redirect(data["redirectUrl"]);
                


            }
            else
            {

                var auth = new RouteAuthorization()
                {
                    ClientId = ctx.Request.Query["clientid"].FirstOrDefault(),
                };

                var aut = await ctx.AuthenticateAsync(AuthenticationSchema);
                if (aut.Succeeded)
                {
                    try
                    {
                        var app = auth.BuildApp(new Uri(ctx.Request.GetDisplayUrl()).Host,msalTokenCacheProvider);

                        var account = MsalAccount.FromMsalAccountId(aut.Principal.FindFirstValue("sub"));

                        await app.AcquireTokenSilent(new[] { "profile" }, account)
                            .ExecuteAsync();



                        ctx.Response.Redirect(ctx.Request.Query["redirectUri"].FirstOrDefault());
                        return;
                    }
                    catch (MsalUiRequiredException ex)
                    {

                    }

                }




                ctx.Response.Redirect(await auth.GetRedirectUrl(new Uri(ctx.Request.GetDisplayUrl()).Host,ctx.Request.Query["redirectUri"].FirstOrDefault()));
               
            }

        }

        
    }
}