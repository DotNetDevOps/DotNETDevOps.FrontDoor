using DotNETDevOps.Extensions.AzureFunctions;
using DotNETDevOps.FrontDoor.FrontFunction;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;


[assembly: WebJobsStartup(typeof(AspNetCoreWebHostStartUp<WebBuilder, Startup>))]

namespace DotNETDevOps.FrontDoor.FrontFunction
{
    public class WebBuilder : IWebHostBuilderExtension<Startup>
    {
        private readonly IHostingEnvironment environment;

        public WebBuilder(IHostingEnvironment environment)
        {
            this.environment = environment;
        }
        public void ConfigureAppConfiguration(WebHostBuilderContext context, IConfigurationBuilder builder)
        {

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

        public void ConfigureWebHostBuilder(ExecutionContext executionContext, IWebHostBuilder builder, IServiceProvider serviceProvider)
        {
            builder.ConfigureAppConfiguration(ConfigureAppConfiguration);
            builder.ConfigureLogging(Logging);

            if (environment.IsDevelopment())
            {
                builder.UseContentRoot(Path.Combine(Directory.GetCurrentDirectory(), "../../.."));
            }
        }


    }
    public class ServerlessApi
    {


        [FunctionName("AspNetCoreHost")]
        public Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, Route = "{*all}")]HttpRequest req,
            [AspNetCoreRunner(Startup = typeof(Startup))] IAspNetCoreRunner aspNetCoreRunner,
            ExecutionContext executionContext)
        {

            return aspNetCoreRunner.RunAsync(executionContext);
        }
    }
}
