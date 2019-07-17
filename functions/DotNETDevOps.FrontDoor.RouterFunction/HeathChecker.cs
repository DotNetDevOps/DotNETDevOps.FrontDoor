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
        private readonly IHostingEnvironment environment;

        public HeathChecker(HealthCheckManager hostNameResolver, IHostingEnvironment environment)
        {
            this.hostNameResolver = hostNameResolver;
            this.environment = environment;
        }
        [FunctionName("HeathChecker")]
        public  Task Run([TimerTrigger("*/10 * * * * *")]TimerInfo myTimer, ILogger log)
        {
            if (!environment.IsDevelopment())
            {
                return hostNameResolver.Healthcheck();
            }

            return Task.CompletedTask;
        }
    }
}
