using System;
using DotNETDevOps.Extensions.AzureFunctions;
using Microsoft.Azure.WebJobs.Hosting;


namespace DotNETDevOps.FrontDoor.RouterFunction
{
    public class PrefixRoute : BaseRoute
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
