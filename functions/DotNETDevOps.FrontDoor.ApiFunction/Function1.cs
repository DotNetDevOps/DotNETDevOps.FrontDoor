using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using DotNETDevOps.Extensions.AzureFunctions;
using Microsoft.Azure.WebJobs.Hosting;

[assembly: WebJobsStartup(typeof(AspNetCoreWebHostStartUp))]

namespace DotNETDevOps.FrontDoor.ApiFunction
{
     
    public class ServerlessApi
    {
        
        private readonly IAspNetCoreRunner<ServerlessApi> aspNetCoreRunner;

        public ServerlessApi(IAspNetCoreRunner<ServerlessApi> aspNetCoreRunner)
        {
            this.aspNetCoreRunner = aspNetCoreRunner;
        }

        [FunctionName("AspNetCoreHost")]
        public Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, Route = "{*all}")]HttpRequest req, ExecutionContext executionContext)
            => aspNetCoreRunner.RunAsync<Startup>(req, executionContext);
    }
}
