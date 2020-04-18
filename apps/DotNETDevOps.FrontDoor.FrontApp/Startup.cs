using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using DotNETDevOps.FrontDoor.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProxyKit;
namespace DotNETDevOps.FrontDoor.FrontApp
{
    public class VueService : BackgroundService
    {
        private readonly Microsoft.Extensions.Hosting.IHostingEnvironment hostingEnvironment;
        private readonly ILogger<VueService> logger;
        private readonly Microsoft.Extensions.Hosting.IApplicationLifetime applicationLifetime;

        public VueService(Microsoft.Extensions.Hosting.IHostingEnvironment hostingEnvironment, ILogger<VueService> logger, Microsoft.Extensions.Hosting.IApplicationLifetime applicationLifetime)
        {
            this.hostingEnvironment = hostingEnvironment;
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.applicationLifetime = applicationLifetime;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return;
            System.Diagnostics.Process process = new System.Diagnostics.Process()
            {
               // EnableRaisingEvents = true
            };

            try
            {

                System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
                // startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                startInfo.FileName = "node";
                startInfo.Arguments = "node_modules/@vue/cli-service/bin/vue-cli-service.js serve";
                startInfo.WorkingDirectory = hostingEnvironment.ContentRootPath;
                process.StartInfo = startInfo;

                // redirect the output
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                // hookup the eventhandlers to capture the data that is received
                process.OutputDataReceived += (sender, args) => logger.LogInformation(args.Data);
                process.ErrorDataReceived += (sender, args) => logger.LogError(args.Data);


              

                // direct start
                process.StartInfo.UseShellExecute = false;

                process.Start();

                // start our event pumps
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();


                while (!(stoppingToken.IsCancellationRequested || applicationLifetime.ApplicationStopping.IsCancellationRequested))
                {
                    await Task.Delay(1000);
                }

            }catch(Exception ex)
            {

            }
            finally
            {
                try
                {
                    process.Kill();
                }
                catch (Exception)
                {

                }
            }
        }
    }
    public class Startup
    {
        private readonly Microsoft.Extensions.Hosting.IHostingEnvironment env;

        public Startup(Microsoft.Extensions.Hosting.IHostingEnvironment env)
        {
            this.env = env;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            if (env.IsDevelopment())
            {
                 
                services.AddHostedService<VueService>();
                services.AddProxy(httpClientBuilder =>
                {
                    httpClientBuilder.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback = (_, __, ___, _____) => true
                    });
                });
            }

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app)
        {

           

            if (env.IsDevelopment())
            {
                app.UseWebSockets();


                app.UseDeveloperExceptionPage();

                //app.Use(async (ctx, next) =>
                //{
                //    if (ctx.WebSockets.IsWebSocketRequest)
                //    {
                        
                //        var host = new Uri("https://localhost:8080");

                //        await WebSocketHelpers.AcceptProxyWebSocketRequest(
                //            ctx, 
                //            new Uri((ctx.Request.IsHttps ? "wss://" : "ws://") + host.Host + (host.IsDefaultPort ? "" : ":" + host.Port) + ctx.Request.GetEncodedPathAndQuery()),
                //            option=>option.RemoteCertificateValidationCallback = IgnoreCert);
                       
                //    }
                //    else
                //    {
                //        await next();
                //    }

                //});
                app.RunProxy(h => h.ForwardTo("https://localhost:8080").Send());
            }

            app.UseStaticFiles();
        }

        private bool IgnoreCert(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }
    }
}
