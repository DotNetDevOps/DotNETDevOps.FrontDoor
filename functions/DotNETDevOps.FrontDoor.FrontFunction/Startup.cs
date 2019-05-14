using DotNETDevOps.FrontDoor.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotNETDevOps.FrontDoor.FrontFunction
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
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
 

            if (!env.IsProduction())
            {
                app.UseDeveloperExceptionPage();
                // to do - wire in our HTTP endpoints
                app.Use(async (ctx, next) =>
                {
                    if (Path.GetExtension(ctx.Request.Path) == ".ts" || Path.GetExtension(ctx.Request.Path) == ".tsx")
                    {

                        await ctx.Response.WriteAsync(File.ReadAllText(ctx.Request.Path.Value.Substring(1)));
                    }
                    else
                    {
                        await next();
                    }

                });
            }


            

            //app.UseCors();
            //app.UseStaticFiles();


            //app.UseMvc();

            app.Run(WriteRequestInfo);
        }

        private async Task WriteRequestInfo(HttpContext context)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Unknown Domain:");
            sb.AppendLine($"Request Url: {UriHelper.GetDisplayUrl( context.Request)}");
            sb.AppendLine($"Request Method: {context.Request.Method}");
            sb.AppendLine($"Status Code: {context.Response.StatusCode}");
            sb.AppendLine($"Remote Address: {context.Connection.RemoteIpAddress}");
            sb.AppendLine();
            sb.AppendLine($"Request Headers:");
            foreach(var headers in context.Request.Headers)
            {
                sb.AppendLine($"  {headers.Key}: {string.Join(",",headers.Value)}");
            }

            sb.AppendLine($"Response Headers:");
            foreach (var headers in context.Response.Headers)
            {
                sb.AppendLine($"  {headers.Key}: {string.Join(",", headers.Value)}");
            }

            await context.Response.WriteAsync(sb.ToString());
        }
    }
}
