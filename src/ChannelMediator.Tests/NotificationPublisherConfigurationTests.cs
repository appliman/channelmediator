namespace ChannelMediator.Tests;

public class NotificationPublisherConfigurationTests
{
    [Fact]
    public void DefaultStrategy_IsSequential()
    {
        // Arrange & Act
        var config = new ChannelMediatorConfiguration();

        // Assert
        Assert.Equal(NotificationPublishStrategy.Sequential, config.Strategy);
    }

    [Fact]
    public void Strategy_CanBeSetToParallel()
    {
        // Arrange
        var config = new ChannelMediatorConfiguration();

        // Act
        config.Strategy = NotificationPublishStrategy.Parallel;

        // Assert
        Assert.Equal(NotificationPublishStrategy.Parallel, config.Strategy);
    }

    [Fact]
    public void Strategy_CanBeSetToSequential()
    {
        // Arrange
        var config = new ChannelMediatorConfiguration
        {
            Strategy = NotificationPublishStrategy.Parallel
        };

        // Act
        config.Strategy = NotificationPublishStrategy.Sequential;

        // Assert
        Assert.Equal(NotificationPublishStrategy.Sequential, config.Strategy);
    }
}

public class NotificationPublishStrategyTests
{
    [Fact]
    public void Sequential_HasCorrectValue()
    {
        // Assert
        Assert.Equal((NotificationPublishStrategy)0, NotificationPublishStrategy.Sequential);
    }

    [Fact]
    public void Parallel_HasCorrectValue()
    {
        // Assert
        Assert.Equal((NotificationPublishStrategy)1, NotificationPublishStrategy.Parallel);
    }
}
