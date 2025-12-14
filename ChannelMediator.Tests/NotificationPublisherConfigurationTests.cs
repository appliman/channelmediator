namespace ChannelMediator.Tests;

public class NotificationPublisherConfigurationTests
{
    [Fact]
    public void DefaultStrategy_IsSequential()
    {
        // Arrange & Act
        var config = new ChannelMediatorConfiguration();

        // Assert
        config.Strategy.Should().Be(NotificationPublishStrategy.Sequential);
    }

    [Fact]
    public void Strategy_CanBeSetToParallel()
    {
        // Arrange
        var config = new ChannelMediatorConfiguration();

        // Act
        config.Strategy = NotificationPublishStrategy.Parallel;

        // Assert
        config.Strategy.Should().Be(NotificationPublishStrategy.Parallel);
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
        config.Strategy.Should().Be(NotificationPublishStrategy.Sequential);
    }
}

public class NotificationPublishStrategyTests
{
    [Fact]
    public void Sequential_HasCorrectValue()
    {
        // Assert
        NotificationPublishStrategy.Sequential.Should().Be((NotificationPublishStrategy)0);
    }

    [Fact]
    public void Parallel_HasCorrectValue()
    {
        // Assert
        NotificationPublishStrategy.Parallel.Should().Be((NotificationPublishStrategy)1);
    }
}
