using DotNETDevOps.JsonFunctions;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace DotNETDevOps.FrontDoor.RouterApp
{
    public class CorsFunctions : IExpressionFunctionFactory<CorsPolicyBuilder>
    {
        public ExpressionParser<CorsPolicyBuilder>.ExpressionFunction Get(string name)
        {
            //CorsPolicyBuilder().AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()
            switch (name)
            {
                case "CorsPolicyBuilder":
                    return CorsPolicyBuilder;
                case "AllowAnyOrigin":
                    return AllowAnyOrigin;
                case "AllowAnyMethod":
                    return AllowAnyMethod;
                case "AllowAnyHeader":
                    return AllowAnyHeader;
                case "WithExposedHeaders":
                    return WithExposedHeaders;
                case nameof(AllowCredentials):
                    return AllowCredentials;
                default:
                    throw new NotImplementedException();
            }
        }
        public Task<JToken> CorsPolicyBuilder(CorsPolicyBuilder document,JToken[] args)
        {
             
            return Task.FromResult(JToken.FromObject(new { }));
        }
        public Task<JToken> AllowAnyOrigin(CorsPolicyBuilder document, JToken[] args)
        {
          //  document.AllowAnyOrigin();
            document.SetIsOriginAllowed((origin) => true);
         
            return Task.FromResult(args.First());
        }
        public Task<JToken> AllowCredentials(CorsPolicyBuilder document, JToken[] args)
        {
            document.AllowCredentials();

            return Task.FromResult(args.First());
        }
        public Task<JToken> AllowAnyMethod(CorsPolicyBuilder document, JToken[] args)
        {
            document.AllowAnyMethod();
            return Task.FromResult(args.First());
        }
        public Task<JToken> AllowAnyHeader(CorsPolicyBuilder document, JToken[] args)
        {
            document.AllowAnyHeader();
          
            return Task.FromResult(args.First());
        }
        public Task<JToken> WithExposedHeaders(CorsPolicyBuilder document, JToken[] args)
        {
            document.WithExposedHeaders(args.Skip(1).Select(c=>c.ToString()).ToArray());

            return Task.FromResult(args.First());
        }
    }
}
