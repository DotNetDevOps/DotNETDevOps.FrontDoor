using DotNETDevOps.Extensions.AzureFunctions;
using Microsoft.Azure.WebJobs.Hosting;


namespace DotNETDevOps.FrontDoor.RouterFunction
{
    public class RouteOptions
    {
        public RouteConfig[] Routes{get;set;}
    }
}
