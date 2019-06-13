using Microsoft.Extensions.Hosting;
using System.Collections.Generic;

namespace DotNETDevOps.FrontDoor.RouterApp
{
    public interface IRouteOptionsFactory : IHostedService
    {
        Dictionary<string, BaseRoute[]> GetRoutes();

    }
}
