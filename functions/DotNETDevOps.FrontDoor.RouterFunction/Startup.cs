using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using DotNETDevOps.Extensions.AzureFunctions;
using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using ProxyKit;
using Newtonsoft.Json.Linq;
using System.Net.Http;

[assembly: WebJobsStartup(typeof(AspNetCoreWebHostStartUp))]
[assembly: WebJobsStartup(typeof(DotNETDevOps.FrontDoor.RouterFunction.WebHostBuilderConfigurationBuilderExtension))]

namespace DotNETDevOps.FrontDoor.RouterFunction
{
    public class Startup
    {
        private readonly IHostingEnvironment hostingEnvironment;

        public Startup(IHostingEnvironment hostingEnvironment)
        {
            this.hostingEnvironment = hostingEnvironment;
        }
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddProxy();
            var routes = JToken.Parse(File.ReadAllText($"routes.{hostingEnvironment.EnvironmentName.ToLower()}.json".Replace(".production",""))).ToObject<RouteRoot>();

            services.AddSingleton(new RouteMatcher(routes));
        }
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
           

           

            app.UseWhen(
                    MatchRoutes,
                    appInner => appInner.RunProxy(BuildProxy));

        }

        private Task<HttpResponseMessage> BuildProxy(HttpContext context)
        {
            var config = context.Features.Get<RouteConfig>();

            return context
                     .ForwardTo(config.Backend)
                     .AddXForwardedHeaders()
                     .Send();
        }

        private bool MatchRoutes(HttpContext arg)
        {
            var findMatch = arg.RequestServices.GetRequiredService<RouteMatcher>().FindMatch(arg);

            if (findMatch != null)
            {
                arg.Features.Set(findMatch);
                return true;
            }

            return false;
            
        }
    }
}
