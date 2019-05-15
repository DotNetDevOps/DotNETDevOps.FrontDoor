using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.HttpOverrides.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace DotNETDevOps.FrontDoor.AspNetCore
{
    public static class ForwardedExtensions
    {
        public static IServiceCollection WithXForwardedHeaders(this IServiceCollection services)
        {
            return services.AddSingleton<IStartupFilter, UseForwardedHeadersStartupFilter>();
        }
    }
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
                        var parsed = IPEndPointParser.TryParse(XForwardedFor.SelectMany(k => k.Split(',')).First(), out IPEndPoint remoteIP);
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
    //Borrowed from proxykit until released
    public class WebSocketHelpers
    {
        private static readonly HashSet<string> NotForwardedWebSocketHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Connection", "Host", "Upgrade", "Sec-WebSocket-Accept",
            "Sec-WebSocket-Protocol", "Sec-WebSocket-Key", "Sec-WebSocket-Version",
            "Sec-WebSocket-Extensions"
        };
        private const int DefaultWebSocketBufferSize = 4096;
        public static async Task AcceptProxyWebSocketRequest(HttpContext context, Uri destinationUri, Action<ClientWebSocketOptions> options=null)
        {
            using (var client = new ClientWebSocket())
            {

                options?.Invoke(client.Options);


                foreach (var protocol in context.WebSockets.WebSocketRequestedProtocols)
                {
                    client.Options.AddSubProtocol(protocol);
                }

                foreach (var headerEntry in context.Request.Headers)
                {
                    if (!NotForwardedWebSocketHeaders.Contains(headerEntry.Key))
                    {
                        client.Options.SetRequestHeader(headerEntry.Key, headerEntry.Value);
                    }
                }
               
                //if (_options.WebSocketKeepAliveInterval.HasValue)
                //{
                //    client.Options.KeepAliveInterval = _options.WebSocketKeepAliveInterval.Value;
                //}

                try
                {
                    await client.ConnectAsync(destinationUri, context.RequestAborted);
                }
                catch (WebSocketException ex)
                {
                    context.Response.StatusCode = 400;
                    //  _logger.LogError(ex, "Error connecting to server");
                    return;
                }

                using (var server = await context.WebSockets.AcceptWebSocketAsync(client.SubProtocol))
                {
                    var bufferSize = DefaultWebSocketBufferSize;
                    await Task.WhenAll(
                        PumpWebSocket(client, server, bufferSize, context.RequestAborted),
                        PumpWebSocket(server, client, bufferSize, context.RequestAborted));
                }
            }
        }

        private static async Task PumpWebSocket(WebSocket source, WebSocket destination, int bufferSize, CancellationToken cancellationToken)
        {
            if (bufferSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferSize));
            }

            var buffer = new byte[bufferSize];
            while (true)
            {
                WebSocketReceiveResult result;
                try
                {
                    result = await source.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    await destination.CloseOutputAsync(
                        WebSocketCloseStatus.EndpointUnavailable,
                        "Endpoind unavailable",
                        cancellationToken);
                    return;
                }
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await destination.CloseOutputAsync(source.CloseStatus.Value, source.CloseStatusDescription, cancellationToken);
                    return;
                }
                await destination.SendAsync(
                    new ArraySegment<byte>(buffer, 0, result.Count),
                    result.MessageType,
                    result.EndOfMessage,
                    cancellationToken);
            }
        }


    }
}
