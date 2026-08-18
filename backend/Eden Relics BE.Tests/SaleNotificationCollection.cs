namespace Eden_Relics_BE.Tests;

/// <summary>
/// Serialises every test class that touches the sale-notification path.
///
/// The queue inside SaleNotificationBackgroundService and the fake email service's outbox are
/// both process-wide statics — deliberately, since the queue is fed by requests and drained by
/// a singleton. xUnit runs test classes in parallel by default, which would let one class's
/// enqueued products be drained (and counted) by another's assertions.
/// </summary>
[CollectionDefinition(Name)]
public class SaleNotificationCollection
{
    public const string Name = "SaleNotifications";
}
