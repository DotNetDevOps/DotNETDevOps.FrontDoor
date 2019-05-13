using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.WebJobs.Hosting;
using DotNETDevOps.Extensions.AzureFunctions;

[assembly: WebJobsStartup(typeof(AspNetCoreWebHostStartUp))]

namespace DotNETDevOps.FrontDoor.FrontFunction
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
