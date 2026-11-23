using Platform.Messaging.Topology;

namespace Platform.Messaging.Tests;

public class TopologyTests
{
    [Fact]
    public void EveryConsumerQueue_HasDeadLetterQueueName()
    {
        Assert.Equal("loans.catalog-sync.dlq", MessagingTopology.DeadLetterQueue(MessagingTopology.LoansQueue));
        Assert.Equal("reviews.catalog-sync.dlq", MessagingTopology.DeadLetterQueue(MessagingTopology.ReviewsQueue));
    }

    [Fact]
    public void AllQueues_ContainsBothConsumers()
    {
        var queues = MessagingTopology.AllQueues.ToList();

        Assert.Contains(MessagingTopology.LoansQueue, queues);
        Assert.Contains(MessagingTopology.ReviewsQueue, queues);
    }
}
