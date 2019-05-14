using DotNETDevOps.Extensions.AzureFunctions;
using Microsoft.Azure.WebJobs.Hosting;
using System.Text.RegularExpressions;



namespace DotNETDevOps.FrontDoor.RouterFunction
{
    public class RegexRoute : RouteConfig
    {
        private Regex _regex;
        public override void Initialize()
        {
            StopOnMatch = true;
            var caseInsensitive = Route.StartsWith("~*");
           
            if(caseInsensitive)
                _regex = new Regex(Route.Substring(2).TrimStart(),RegexOptions.Compiled| RegexOptions.IgnoreCase| RegexOptions.Singleline);
            else
                _regex = new Regex(Route.Substring(1).TrimStart(), RegexOptions.Compiled | RegexOptions.Singleline);
        }

        public override bool IsMatch(string url)
        {
            return _regex.IsMatch(url);
        }
    }
}
