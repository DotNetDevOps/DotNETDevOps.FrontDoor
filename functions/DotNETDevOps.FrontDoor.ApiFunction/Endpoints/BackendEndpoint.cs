using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace DotNETDevOps.FrontDoor.ApiFunction.Endpoints
{
    [ApiController]
    [Route("/providers/DotNETDevOps.FrontDoor")]
    public class BackendEndpoint
    {
        [HttpPost("backends")]
        public async Task<IActionResult> AddBackendAsync()
        {
            return new OkResult();
        }

        [HttpGet("backends")]
        public async Task<IActionResult> ListBackendAsync(
          [FromServices] IDurableClient durableOrchestrationClient)
        { 
            return new OkResult();
        }
    }
}
