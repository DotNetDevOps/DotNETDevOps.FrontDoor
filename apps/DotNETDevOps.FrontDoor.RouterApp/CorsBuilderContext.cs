using DotNETDevOps.FrontDoor.AspNetCore;
using DotNETDevOps.JsonFunctions;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotNETDevOps.FrontDoor.RouterApp
{
    public class CorsBuilderContext : ICorsPolicyProvider
    {
        public CorsBuilderContext(ILogger<CorsBuilderContext> logger)
        {
            this.logger = logger;
        }
        public Dictionary<string, CorsPolicy> Builders = new Dictionary<string, CorsPolicy>();
        private readonly ILogger<CorsBuilderContext> logger;

        // public string ActiveBuilderName { get;set; }
        // public CorsPolicyBuilder Active => Builders.ContainsKey(ActiveBuilderName) ? Builders[ActiveBuilderName] : Builders[ActiveBuilderName] = new CorsPolicyBuilder();

        public async Task<CorsPolicy> GetPolicyAsync(HttpContext context, string policyName)
        {
            var config = context.Features.Get<BaseRoute>();
            if (!string.IsNullOrEmpty(config.Cors))
            {
               
               
                var policy = await GetOrAddPolicy(config.Cors.ToMD5Hash(), config);
              
                //if(policy.SupportsCredentials && policy.AllowAnyOrigin)
                //{
                //    if(!policy.Origins.Contains(context.Request.Headers["Origin"]))
                //        policy.Origins.Add(context.Request.Headers["Origin"]);            
                //}
                return policy;
                
            }
            return null;

        }

        private async Task<CorsPolicy> GetOrAddPolicy(string policyName, BaseRoute config)
        {
            if (!Builders.ContainsKey(policyName))
            {
                var ex = new ExpressionParser<CorsPolicyBuilder>(Options.Create(new ExpressionParserOptions<CorsPolicyBuilder>
                {
                    ThrowOnError = false,
                    Document = new CorsPolicyBuilder()
                }), logger, new CorsFunctions());

                await ex.EvaluateAsync(config.Cors);

                return Builders[policyName] = ex.Document.Build();
            }

            return Builders[policyName];
             
        }
    }
}
