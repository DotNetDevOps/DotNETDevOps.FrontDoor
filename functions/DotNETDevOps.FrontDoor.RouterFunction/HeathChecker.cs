using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DotNETDevOps.Extensions.AzureFunctions.HealthCheck;
using DotNETDevOps.FrontDoor.RouterFunction;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;



namespace DotNETDevOps.FrontDoor.RouterFunction
{
  
    
    
    public class HeathChecker
    {
        private readonly HealthCheckManager hostNameResolver;

        public HeathChecker(HealthCheckManager hostNameResolver)
        {
            this.hostNameResolver = hostNameResolver;
        }
        [FunctionName("HeathChecker")]
        public  Task Run([TimerTrigger("*/10 * * * * *")]TimerInfo myTimer, ILogger log)
        {
            return hostNameResolver.Healthcheck(); 
        }
    }
}
