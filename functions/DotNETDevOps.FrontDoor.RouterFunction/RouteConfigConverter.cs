﻿using System;
using Newtonsoft.Json;
using DotNETDevOps.Extensions.AzureFunctions;
using Microsoft.Azure.WebJobs.Hosting;
using Newtonsoft.Json.Linq;



namespace DotNETDevOps.FrontDoor.RouterFunction
{
    public class RouteConfigConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            throw new NotImplementedException();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var obj = JToken.ReadFrom(reader);
            BaseRoute route = null;
            var routeStr = obj.SelectToken("$.route").ToString();
            if (routeStr.StartsWith("="))
            {
                route = new ExactRoute();
            }else if (routeStr.StartsWith("/") ||routeStr.StartsWith("^~"))
            {
                route = new PrefixRoute();
            }else if (routeStr.StartsWith("~"))
            {
                route = new RegexRoute();
            }

            serializer.Populate(obj.CreateReader(), route);

            route.Initialize();

            return route;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
           
        }
    }
}
