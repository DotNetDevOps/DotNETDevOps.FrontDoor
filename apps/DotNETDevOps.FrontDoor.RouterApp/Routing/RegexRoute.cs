﻿
using System.Text.RegularExpressions;



namespace DotNETDevOps.FrontDoor.RouterApp
{
    public class RegexRoute : BaseRoute
    {
        public override int Precedence => 1000;

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
