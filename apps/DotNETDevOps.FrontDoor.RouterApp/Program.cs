using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DotNETDevOps.FrontDoor.RouterApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateWebHostBuilder(args).Build().Run();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>()
             .UseKestrel(options =>
             {
                 options.Listen(IPAddress.Loopback, 12500, listenOptions =>
                 {
                     listenOptions.UseHttps("c:/dev/CN=dotnetdevops.eu.ngrok.io.pfx", "");
                 });
             });
    }
}
