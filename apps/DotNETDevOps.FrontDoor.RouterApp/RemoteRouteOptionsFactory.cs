using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace DotNETDevOps.FrontDoor.RouterApp
{

    public class RemoteRouteOptionsFactory : DefaultRouteOptionsFactory
    {
        private readonly IHttpClientFactory httpClientFactory;
        private readonly IConfiguration configuration;

        private readonly AsyncExpiringLazy<string> etag;

        public RemoteRouteOptionsFactory(ILogger<FileSystemRouteOptionsFactory> logger, IHttpClientFactory httpClientFactory, IConfiguration configuration, HealthCheckRunner healthCheckRunner) : base(logger,healthCheckRunner)
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
            Task.Run(() => etag.Value());

            return base.GetRoutes();
        }
    }
}
