using DotNETDevOps.Extensions.AzureFunctions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Threading.Tasks;

[assembly: WebJobsStartup(typeof(AspNetCoreWebHostStartUp))]
[assembly: WebJobsStartup(typeof(DotNETDevOps.FrontDoor.RouterFunction.WebHostBuilderConfigurationBuilderExtension))]

namespace DotNETDevOps.FrontDoor.RouterFunction
{

    public class WebHostBuilderConfigurationBuilderExtension : IWebHostBuilderExtension, IWebJobsStartup
    {
        private readonly IHostingEnvironment hostingEnvironment;

        public WebHostBuilderConfigurationBuilderExtension(IHostingEnvironment hostingEnvironment)
        {
            this.hostingEnvironment = hostingEnvironment;
        }
        public void Configure(IWebJobsBuilder builder)
        {
            builder.Services.AddSingleton<WebHostBuilderConfigurationBuilderExtension>();
        }

        public void ConfigureAppConfiguration(WebHostBuilderContext context, IConfigurationBuilder builder)
        {
            //builder.AddJsonFile("appsettings.json");
        }

        public void ConfigureWebHostBuilder(WebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration(ConfigureAppConfiguration);
            builder.ConfigureLogging(Logging);

            if (hostingEnvironment.IsDevelopment())
            {
                builder.UseContentRoot(Path.Combine(Directory.GetCurrentDirectory(), "../../.."));
            }
           // builder.UseContentRoot();
            //   builder.UseContentRoot(Directory.GetCurrentDirectory());
            // builder.UseContentRoot();
        }

        private void Logging(ILoggingBuilder b)
        {
            //b.AddProvider(new SerilogLoggerProvider(
            //            new LoggerConfiguration()
            //               .MinimumLevel.Verbose()
            //               .MinimumLevel.Override("Microsoft", LogEventLevel.Verbose)
            //               .Enrich.FromLogContext()
            //                .WriteTo.File($"apptrace.log", buffered: true, flushToDiskInterval: TimeSpan.FromSeconds(30), rollOnFileSizeLimit: true, fileSizeLimitBytes: 1024 * 1024 * 32, rollingInterval: RollingInterval.Hour)
            //               .CreateLogger()));
        }
    }


    [WebHostBuilder(typeof(WebHostBuilderConfigurationBuilderExtension))]
    public class ServerlessApiFunction
    {
      

        private readonly IAspNetCoreRunner<ServerlessApiFunction> aspNetCoreRunner;

        public ServerlessApiFunction(IAspNetCoreRunner<ServerlessApiFunction> aspNetCoreRunner)
        {
            this.aspNetCoreRunner = aspNetCoreRunner;
        }

        [FunctionName("AspNetCoreHost")]
        public Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, Route = "{*all}")]HttpRequest req, ExecutionContext executionContext)
            => aspNetCoreRunner.RunAsync<Startup>(req, executionContext);
    }
}
