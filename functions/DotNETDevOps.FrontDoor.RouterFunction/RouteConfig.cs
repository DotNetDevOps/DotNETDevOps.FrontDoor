using Newtonsoft.Json;
using DotNETDevOps.Extensions.AzureFunctions;
using Microsoft.Azure.WebJobs.Hosting;

[assembly: WebJobsStartup(typeof(AspNetCoreWebHostStartUp))]
[assembly: WebJobsStartup(typeof(DotNETDevOps.FrontDoor.RouterFunction.WebHostBuilderConfigurationBuilderExtension))]

namespace DotNETDevOps.FrontDoor.RouterFunction
{
    [JsonConverter(typeof(RouteConfigConverter))]
    public abstract class RouteConfig
    {
        public string Route { get; set; }
        public string Backend { get; set; }

        public abstract bool IsMatch(string url);

        public bool StopOnMatch { get; protected set; }

        public abstract void Initialize();
    }
}
