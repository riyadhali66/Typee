namespace Typee.Core.Models;

public sealed class EndpointAcknowledgment
{
    public required string ProfileName { get; init; }
    public required string Host { get; init; }
    public DateTimeOffset AcknowledgedUtc { get; init; }
}
