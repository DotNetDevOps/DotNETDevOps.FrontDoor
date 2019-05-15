using System;
using DotNETDevOps.Extensions.AzureFunctions;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Hosting;



namespace DotNETDevOps.FrontDoor.RouterFunction
{
    public class ExactRoute : BaseRoute
    {
        public override int Precedence => 0;
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

        public override void RewriteUrl(HttpContext context)
        {
            if (Prefix.Length > 1)
            {
               
                
                {
                    if (context.Request.Path.StartsWithSegments(Prefix.TrimEnd('/'), out var rest))
                    {
                        if (rest == "/")
                        {
                            context.Request.Path = null;
                        }
                        else
                        {
                            context.Request.Path = rest;
                        }
                    }
                }
            }
        }
    }
}
