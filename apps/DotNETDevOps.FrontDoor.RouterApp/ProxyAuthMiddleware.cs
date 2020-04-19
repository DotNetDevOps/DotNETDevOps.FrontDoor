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
        private readonly DynamicCookieScheme dynamicCookieScheme;

        public ProxyAuthTokenMiddleware(RequestDelegate next, KeyVaultProvider keyVaultProvider, IMsalTokenCacheProvider msalTokenCacheProvider, DynamicCookieScheme dynamicCookieScheme)
        {
            _next = next;
            this.keyVaultProvider = keyVaultProvider;
            this.msalTokenCacheProvider = msalTokenCacheProvider;
            this.dynamicCookieScheme = dynamicCookieScheme;
        }

        public async Task InvokeAsync(HttpContext ctx)
        {
            var config = ctx.Features.Get<BaseRoute>();
            if (config.Authoriztion != null && !ctx.Request.Headers.ContainsKey("Authorization"))
            {
                var schema = await dynamicCookieScheme.EnsureAddedAsync(ctx.Request.Headers["X-ClientId"]);

                AuthenticationResult result = null;
                
                var auth = await ctx.AuthenticateAsync(schema);
                if (!auth.Succeeded)
                {
                    ctx.Response.StatusCode = 401;
                    return;
                }
                ;

                var app = await RouteAuthorization.BuildAppAsync(
                    new Uri(ctx.Request.GetDisplayUrl()).Host,
                    auth.Principal.FindFirstValue("clientid"), 
                    msalTokenCacheProvider,
                    keyVaultProvider
                    );



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
                  //  var accountClean = await app.GetAccountAsync(account.HomeAccountId.Identifier);
                    await app.RemoveAsync(account);
                    await ctx.SignOutAsync(schema);
                    ctx.Response.StatusCode = 401;

                    return;
                }



            }


            await _next(ctx);
        }
        
    }
    public class ProxyAuthMiddleware
    {
        public const string AuthenticationSchema = "ProxyAuth";
        private readonly RequestDelegate _next;
        private readonly KeyVaultProvider keyVaultProvider;
        private readonly IMsalTokenCacheProvider msalTokenCacheProvider;
        private readonly DynamicCookieScheme dynamicCookieScheme;

        public ProxyAuthMiddleware(RequestDelegate next, KeyVaultProvider keyVaultProvider, IMsalTokenCacheProvider msalTokenCacheProvider, DynamicCookieScheme dynamicCookieScheme)
        {
            _next = next;
            this.keyVaultProvider = keyVaultProvider;
            this.msalTokenCacheProvider = msalTokenCacheProvider;
            this.dynamicCookieScheme = dynamicCookieScheme;
        }

        public async Task InvokeAsync(HttpContext ctx)
        {
            if (ctx.Request.Path.Value.EndsWith("callback"))
            {
                var data = HttpUtility.ParseQueryString(Encoding.ASCII.GetString(Base64Url.Decode(ctx.Request.Query["state"].FirstOrDefault())));
                var schema = await dynamicCookieScheme.EnsureAddedAsync(data["clientid"]);
                var path = data["path"];
              //  var secret = await keyVaultProvider.GetValueAsync(data["clientid"]);
 
                var app = await RouteAuthorization.BuildAppAsync(new Uri(ctx.Request.GetDisplayUrl()).Host, data["clientid"],  msalTokenCacheProvider, keyVaultProvider);

                var token = await app.AcquireTokenByAuthorizationCode(new string[] { "profile" }, ctx.Request.Query["code"].FirstOrDefault()).ExecuteAsync();
                
                await ctx.SignInAsync(schema, new ClaimsPrincipal(
                    new ClaimsIdentity(new Claim[]{
                            new Claim("sub",$"{token.Account.HomeAccountId.ObjectId}.{token.Account.HomeAccountId.TenantId}"),
                            new Claim("clientid",data["clientid"])
                        }, schema)),
                        new AuthenticationProperties
                        {
                            IsPersistent = true,
                            AllowRefresh = true,
                            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14),
                            Parameters =
                            {
                                { "path",path }
                            }
                        });

                ctx.Response.Redirect(data["redirectUrl"]);
                


            }
            else
            {


                var schema = await dynamicCookieScheme.EnsureAddedAsync(ctx.Request.Query["clientid"].FirstOrDefault());
                var aut = await ctx.AuthenticateAsync(schema);
                if (aut.Succeeded)
                {
                   
                        var app = await RouteAuthorization.BuildAppAsync(new Uri(ctx.Request.GetDisplayUrl()).Host, aut.Principal.FindFirstValue("clientid"), msalTokenCacheProvider,keyVaultProvider);

                        var account = MsalAccount.FromMsalAccountId(aut.Principal.FindFirstValue("sub"));
                 
                    try
                    {
                        await app.AcquireTokenSilent(new[] { "profile" }, account)
                            .ExecuteAsync();



                        ctx.Response.Redirect(ctx.Request.Query["redirectUri"].FirstOrDefault());
                        return;
                    }
                    catch (MsalUiRequiredException ex)
                    {

                    }catch(Exception ex)
                    {
                        await app.RemoveAsync(account);
                        await ctx.SignOutAsync(schema);
                    }

                }




                ctx.Response.Redirect(await RouteAuthorization.GetRedirectUrl(
                   ctx.Request.GetDisplayUrl(),
                    ctx.Request.Query["clientid"].FirstOrDefault(),
                    ctx.Request.Query["redirectUri"].FirstOrDefault(), null));
               
            }

        }

        
    }
}