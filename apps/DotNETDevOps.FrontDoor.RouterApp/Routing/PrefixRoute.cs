using Microsoft.AspNetCore.Http;
using System;


namespace DotNETDevOps.FrontDoor.RouterApp
{
    public class PrefixRoute : BaseRoute
    {
        public override int Precedence => StopOnMatch ? 10 : 100;
        

        public string Prefix { get; private set; }
        
        public override void Initialize()
        {
            if (Route.StartsWith("^~"))
            {
                Prefix = Route.Substring(2).TrimStart();
                StopOnMatch = true;
            }
            else
            {
                Prefix = Route.TrimStart();
               
            }
            RelativePrecedence = Prefix.Length;
        }

        public override bool IsMatch(string url)
        {
            return url.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);
        }

        public override void RewriteUrl(HttpContext context)
        {
            if (context.Request.Path.StartsWithSegments(Prefix.TrimEnd('/'),out var rest))
            {
                context.Request.Path = rest;
            }
        }
    }
}
