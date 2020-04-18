

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using System;
using System.Linq;
using System.Net;

namespace DotNETDevOps.FrontDoor.AspNetCore
{

    public class UseForwardedHeadersStartupFilter : IStartupFilter
    {
        private const string XForwardedPathBase = "X-Forwarded-PathBase";

        private readonly ILogger logger;

        public UseForwardedHeadersStartupFilter(ILogger<UseForwardedHeadersStartupFilter> logger)
        {


            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> nextBuilder)
        {
            return builder =>
            {
                var options = new ForwardedHeadersOptions
                {
                    ForwardedHeaders = ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto,

                };

                builder.UseForwardedHeaders(options);

                builder.Use(async (context, next) =>
                {


                    if (context.Request.Headers.TryGetValue("X-Forwarded-For", out StringValues XForwardedFor))
                    {
#if NETCOREAPP3_1
                        var parsed = IPEndPoint.TryParse(XForwardedFor.SelectMany(k => k.Split(',')).First(), out IPEndPoint remoteIP);
#else
                       
                         var parsed = Microsoft.AspNetCore.HttpOverrides.Internal.IPEndPointParser.TryParse(XForwardedFor.SelectMany(k => k.Split(',')).First(), out IPEndPoint remoteIP);
#endif
                        //   
                        if (parsed)
                        {
                            context.Connection.RemoteIpAddress = remoteIP.Address;
                        }
                        else
                        {
                            logger.LogInformation("X-Forwarded-For = {XForwardedFor} {Length}, Parsed={parsed}, remoteIp={oldRemoteIP}/{remoteIP}", string.Join(",", XForwardedFor), XForwardedFor.Count, parsed, context.Connection.RemoteIpAddress, parsed ? remoteIP : null);
                        }
                    }
                    else
                    {
                        logger.LogInformation("No X-Forwarded-For: {Request} {Path} {REmoteIpAddress} ", context.Request.Host, context.Request.Path, context.Connection.RemoteIpAddress);
                    }

                    var original = context.Request.PathBase;
                    try
                    {
                        if (context.Request.Headers.TryGetValue(XForwardedPathBase, out var value) && value.FirstOrDefault() != "/")
                        {
                            context.Request.PathBase = value.First();
                        }


                        await next();


                    }
                    finally
                    {
                        context.Request.Path = context.Request.PathBase + context.Request.Path;
                        context.Request.PathBase = original;

                    }

                });

                nextBuilder(builder);


            };
        }

    }
}
