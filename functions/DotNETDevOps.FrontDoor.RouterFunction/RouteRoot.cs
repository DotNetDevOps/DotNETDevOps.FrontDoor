using DotNETDevOps.Extensions.AzureFunctions;
using Microsoft.Azure.WebJobs.Hosting;

[assembly: WebJobsStartup(typeof(AspNetCoreWebHostStartUp))]
[assembly: WebJobsStartup(typeof(DotNETDevOps.FrontDoor.RouterFunction.WebHostBuilderConfigurationBuilderExtension))]

namespace DotNETDevOps.FrontDoor.RouterFunction
{
    public class RouteRoot
    {
        public RouteConfig[] Routes{get;set;}
    }
}
