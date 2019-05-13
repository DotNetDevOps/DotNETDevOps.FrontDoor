using System;
using DotNETDevOps.Extensions.AzureFunctions;
using Microsoft.Azure.WebJobs.Hosting;

[assembly: WebJobsStartup(typeof(AspNetCoreWebHostStartUp))]
[assembly: WebJobsStartup(typeof(DotNETDevOps.FrontDoor.RouterFunction.WebHostBuilderConfigurationBuilderExtension))]

namespace DotNETDevOps.FrontDoor.RouterFunction
{
    public class PrefixRoute : RouteConfig
    {
        public string Prefix { get; private set; }
        
        public override void Initialize()
        {
            if (Route.StartsWith("^~"))
            {
                Prefix = Route.Substring(2).TrimStart();
                StopOnMatch = true;
            }
            else
            {
                Prefix = Route.TrimStart();
               
            }
        }

        public override bool IsMatch(string url)
        {
            return url.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);
        }
    }
}
