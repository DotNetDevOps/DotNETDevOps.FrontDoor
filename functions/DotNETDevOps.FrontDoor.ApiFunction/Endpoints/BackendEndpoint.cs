using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace DotNETDevOps.FrontDoor.ApiFunction.Endpoints
{
    [ApiController]
    public class BackendEndpoint
    {
        [HttpPost("/providers/DotNETDevOps.FrontDoor/backends")]
        public async Task<IActionResult> AddBackendAsync()
        {
            return new OkResult();
        }
        [HttpGet("/providers/DotNETDevOps.FrontDoor/backends")]
        public async Task<IActionResult> ListBackendAsync()
        {

            return new OkResult();
        }
    }
}
