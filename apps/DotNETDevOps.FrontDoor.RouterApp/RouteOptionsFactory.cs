using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNETDevOps.FrontDoor.RouterApp
{
    public interface IRouteOptionsFactory
    {
        Dictionary<string, BaseRoute[]> GetRoutes();

    }
    public class FileSystemRouteOptionsFactory : DefaultRouteOptionsFactory
    {
        private readonly IHostingEnvironment hostingEnvironment;

        public FileSystemRouteOptionsFactory(ILogger<FileSystemRouteOptionsFactory> logger, IHostingEnvironment hostingEnvironment): base(logger) { 
            this.hostingEnvironment = hostingEnvironment;
        }
        public override async Task<JToken> GetRouteConfiguration()
        {
            var routes = JToken.Parse(await File.ReadAllTextAsync(Path.Combine(this.hostingEnvironment.ContentRootPath, $"routes.{hostingEnvironment.EnvironmentName.ToLower()}.json".Replace(".production", ""))));

            return routes;
        }
    }

    public class RemoteRouteOptionsFactory : DefaultRouteOptionsFactory
    {
        private readonly IHttpClientFactory httpClientFactory;
        private readonly IConfiguration configuration;

        private readonly AsyncExpiringLazy<string> etag;

        public RemoteRouteOptionsFactory(ILogger<FileSystemRouteOptionsFactory> logger, IHttpClientFactory httpClientFactory, IConfiguration configuration) : base(logger)
        {
            this.httpClientFactory = httpClientFactory;
            this.configuration = configuration;

            etag = new AsyncExpiringLazy<string>(async (old) =>
              {


                  var http = httpClientFactory.CreateClient();
                  var data = await http.SendAsync(new HttpRequestMessage(HttpMethod.Head, configuration.GetValue<string>("RemoteConfiguration")));

                  if (data.Headers.TryGetValues("etag",out var etags))
                  {
                      var etag = etags.FirstOrDefault();
                      if (etag != old.Result)
                      {
                          await this.Invalidate();
                      }

                      return new ExpirationMetadata<string>
                      {
                          ValidUntil = DateTimeOffset.UtcNow.AddMinutes(1),
                          Result = etag
                      };

                  }
                  return new ExpirationMetadata<string>
                  {
                      ValidUntil = DateTimeOffset.UtcNow.AddMinutes(60),
                      Result = null
                  };


              });
        }
        public override async Task<JToken> GetRouteConfiguration()
        {
            var http = httpClientFactory.CreateClient();
            var data = await http.GetStringAsync(configuration.GetValue<string>("RemoteConfiguration"));
            var routes = JToken.Parse(data);

            return routes;
        }

        public override Dictionary<string, BaseRoute[]> GetRoutes()
        {
            return base.GetRoutes();
        }
    }

    public struct ExpirationMetadata<T>
    {
        public T Result { get; set; }

        public DateTimeOffset ValidUntil { get; set; }
    }

    internal class AsyncExpiringLazy<T>
    {
        private readonly SemaphoreSlim _syncLock = new SemaphoreSlim(initialCount: 1);
        private readonly Func<ExpirationMetadata<T>, Task<ExpirationMetadata<T>>> _valueProvider;
        private ExpirationMetadata<T> _value;

        public AsyncExpiringLazy(Func<ExpirationMetadata<T>, Task<ExpirationMetadata<T>>> valueProvider)
        {
            if (valueProvider == null) throw new ArgumentNullException(nameof(valueProvider));
            _valueProvider = valueProvider;
        }

        private bool IsValueCreatedInternal => _value.Result != null && _value.ValidUntil > DateTimeOffset.UtcNow;

        public async Task<bool> IsValueCreated()
        {
            await _syncLock.WaitAsync().ConfigureAwait(false);
            try
            {
                return IsValueCreatedInternal;
            }
            finally
            {
                _syncLock.Release();
            }
        }

        public TaskAwaiter<T> GetAwaiter()
        {
            return Value().GetAwaiter();
        }

        public async Task<T> Value()
        {
            await _syncLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (IsValueCreatedInternal)
                {
                    return _value.Result;
                }

                var result = await _valueProvider(_value).ConfigureAwait(false);
                _value = result;
                return _value.Result;
            }
            finally
            {
                _syncLock.Release();
            }
        }

        public async Task Invalidate()
        {
            await _syncLock.WaitAsync().ConfigureAwait(false);
            _value = default(ExpirationMetadata<T>);
            _syncLock.Release();
        }

    }
    public abstract class DefaultRouteOptionsFactory : IRouteOptionsFactory
    {
        private readonly ILogger logger;
        private readonly AsyncExpiringLazy<Dictionary<string, BaseRoute[]>> routeFactory;
      

        public DefaultRouteOptionsFactory(ILogger logger)
        {
            this.logger = logger;

            this.routeFactory = new AsyncExpiringLazy<Dictionary<string, BaseRoute[]>>(async (old) =>
            {
                try
                {
                    // var ex = new ExpressionParser(Options.Create(new ExpressionParserOptions { ThrowOnError = false, Document = routes }), logger, this);

                    var routes = await GetRouteConfiguration();
                    // Recursive(ex, routes);

                    var routeConfiguration = routes.ToObject<RouteOptions>();


                    var locations = routeConfiguration.Servers.SelectMany(k => k.Locations).ToList();
                    foreach (var server in routeConfiguration.Servers)
                    {
                        foreach (var location in server.Locations)
                        {
                            location.SetServer(routeConfiguration.Upstreams, server);
                        }
                    }


                    return new ExpirationMetadata<Dictionary<string, BaseRoute[]>>
                    {
                        Result = new Dictionary<string, BaseRoute[]>(routeConfiguration.Servers
                         .SelectMany(k => k.Hostnames.Select(h => new { hostname = h, server = k }))
                         .ToLookup(k => k.hostname, v => v.server).ToDictionary(k => k.Key, v => v.SelectMany(k => k.Locations).OrderBy(k => k.Precedence).ThenBy(k => k.RelativePrecedence).ToArray()), StringComparer.OrdinalIgnoreCase),
                        ValidUntil = DateTimeOffset.UtcNow.AddMinutes(10)

                    };

                }catch(Exception ex)
                {
                    return new ExpirationMetadata<Dictionary<string, BaseRoute[]>>
                    {
                        Result = new Dictionary<string, BaseRoute[]>(),
                        ValidUntil = DateTimeOffset.UtcNow.AddSeconds(5)
                   
                    };
                }
            
            });


        }

        public abstract Task<JToken> GetRouteConfiguration();

        
        protected Task Invalidate()
        {
            return this.routeFactory.Invalidate();
        }
        
        public virtual Dictionary<string, BaseRoute[]> GetRoutes()
        {

            var routeTask = routeFactory.Value();
            if (routeTask.IsCompleted)
                return routeTask.Result;

            Task.WaitAll(routeTask);

            return routeTask.Result;  
        }
    }
}
