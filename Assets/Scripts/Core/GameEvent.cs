using System;
namespace CheckmateRPG.Core
{
    [Obsolete("Use typed events inheriting BaseGameEvent<TPayload> instead.")]
    public sealed record GameEvent(object Payload, EventCategory Category = EventCategory.Debug, string Source = "", string Target = "")
        : BaseGameEvent<object>(Payload, Category, Source, Target)
    {
    }
}
