using DotNETDevOps.FrontDoor.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotNETDevOps.FrontDoor.ApiFunction
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc()
               .AddRazorPagesOptions(o => { })
               .SetCompatibilityVersion(Microsoft.AspNetCore.Mvc.CompatibilityVersion.Latest);

            services.AddCors(options =>
            {
                options.AddDefaultPolicy(o => o.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());
            });


            services.WithXForwardedHeaders();
            services.AddHealthChecks();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {

            app.UseHealthChecks("/.well-known/ready", new HealthCheckOptions()
            {
                Predicate = (check) => check.Tags.Contains("ready"),
            });

            app.UseHealthChecks("/.well-known/live", new HealthCheckOptions
            {
                Predicate = (_) => false
            });


            if (!env.IsProduction())
            {
                app.UseDeveloperExceptionPage();
                 
            }

            app.UseCors();
           

            app.UseMvc();
        }
    }
}
