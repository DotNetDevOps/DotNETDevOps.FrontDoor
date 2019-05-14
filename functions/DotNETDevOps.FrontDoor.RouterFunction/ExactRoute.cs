using System;
using DotNETDevOps.Extensions.AzureFunctions;
using Microsoft.Azure.WebJobs.Hosting;



namespace DotNETDevOps.FrontDoor.RouterFunction
{
    public class ExactRoute : RouteConfig
    {
        public string Prefix { get; private set; }
        public override void Initialize()
        {
            Prefix = Route.Substring(1).TrimStart();
            StopOnMatch = true;
        }

        public override bool IsMatch(string url)
        {
            return Prefix.Equals(url,StringComparison.OrdinalIgnoreCase);
        }

       
    }
}
