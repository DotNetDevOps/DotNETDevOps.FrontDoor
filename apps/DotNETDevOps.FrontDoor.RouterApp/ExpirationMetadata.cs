using System;

namespace DotNETDevOps.FrontDoor.RouterApp
{
    public struct ExpirationMetadata<T>
    {
        public T Result { get; set; }

        public DateTimeOffset ValidUntil { get; set; }
    }
}
