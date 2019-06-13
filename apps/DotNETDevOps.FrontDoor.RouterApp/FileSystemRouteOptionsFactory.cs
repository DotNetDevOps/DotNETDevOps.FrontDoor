using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Threading.Tasks;
using IHostingEnvironment = Microsoft.AspNetCore.Hosting.IHostingEnvironment;

namespace DotNETDevOps.FrontDoor.RouterApp
{
    public class FileSystemRouteOptionsFactory : DefaultRouteOptionsFactory
    {
        private readonly IHostingEnvironment hostingEnvironment;

        public FileSystemRouteOptionsFactory(ILogger<FileSystemRouteOptionsFactory> logger, IHostingEnvironment hostingEnvironment, HealthCheckRunner healthCheckRunner) : base(logger, healthCheckRunner) { 
            this.hostingEnvironment = hostingEnvironment;
        }
        public override async Task<JToken> GetRouteConfiguration()
        {
            var routes = JToken.Parse(await File.ReadAllTextAsync(Path.Combine(this.hostingEnvironment.ContentRootPath, $"routes.{hostingEnvironment.EnvironmentName.ToLower()}.json".Replace(".production", ""))));

            return routes;
        }
    }
}
