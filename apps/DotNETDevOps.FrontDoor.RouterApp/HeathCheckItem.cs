using System;

namespace DotNETDevOps.FrontDoor.RouterApp
{
    public class HeathCheckItem :IComparable<HeathCheckItem>
    {
        public DateTimeOffset NextRun { get; set; }
        public string Url { get;  set; }
        public int Interval { get;  set; }
        public int Failed { get;  set; }

        public int CompareTo(HeathCheckItem other)
        {
            return this.NextRun.CompareTo(other.NextRun);
        }
    }
}
