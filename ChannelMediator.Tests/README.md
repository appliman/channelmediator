# ChannelMediator.Tests

This is the comprehensive test suite for the ChannelMediator library.

## Current Coverage

- **Line Coverage**: 98.6%
- **Branch Coverage**: 94.1%
- **Method Coverage**: 100%

The test suite exceeds the minimum 90% code coverage requirement.

## Running Tests

### Run all tests
```bash
dotnet test
```

### Run tests with detailed output
```bash
dotnet test --verbosity normal
```

## Code Coverage

### Generate Coverage Report
```bash
# Run tests with coverage collection
dotnet test --collect:"XPlat Code Coverage" --settings coverlet.runsettings

# Generate HTML report (requires reportgenerator)
reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" -targetdir:"coveragereport" -reporttypes:Html

# Generate text summary
reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" -targetdir:"coveragereport" -reporttypes:TextSummary
```

### Install Report Generator (if not already installed)
```bash
dotnet tool install -g dotnet-reportgenerator-globaltool
```

## Test Structure

### Test Files
- **ChannelMediatorTests.cs**: Tests for the main ChannelMediator class
  - InvokeAsync/Send methods
  - PublishAsync/Publish methods
  - Exception handling
  - Cancellation support
  - Disposal behavior

- **ServiceCollectionExtensionsTests.cs**: Tests for dependency injection registration
  - AddChannelMediator registration
  - AddRequestHandler registration
  - AddPipelineBehavior methods
  - Handler scanning and registration

- **RequestHandlerWrapperTests.cs**: Tests for request handler wrapper
  - Request handling
  - Pipeline behavior execution
  - Cancellation propagation

- **NotificationHandlerWrapperTests.cs**: Tests for notification handler wrapper
  - Notification handling
  - Multiple handler support

- **RequestEnvelopeTests.cs**: Tests for request envelope
  - Dispatch scenarios
  - Cancellation handling
  - Error handling

- **NotificationPublisherConfigurationTests.cs**: Tests for notification configuration

- **IntegrationTests.cs**: End-to-end integration tests
  - Complete workflows
  - High concurrency scenarios
  - Mixed operations
  - Pipeline behaviors

### Test Helpers
- **TestHelpers.cs**: Contains mock requests, handlers, notifications, and behaviors
  - TestRequest/TestResponse
  - TestNotificationHandler1/2
  - LoggingBehavior
  - ValidationBehavior
  - DelayBehavior

## Test Coverage Details

### Covered Components
✅ ChannelMediator core class (100%)
✅ RequestHandlerWrapper (100%)
✅ NotificationHandlerWrapper (100%)
✅ RequestEnvelope (100%)
✅ ServiceCollectionExtensions (98.1%)
✅ NotificationPublisherConfiguration (100%)

### Testing Approach
- **Unit Tests**: Test individual components in isolation
- **Integration Tests**: Test complete workflows and scenarios
- **Edge Cases**: Test error conditions, cancellation, null handling
- **Concurrency Tests**: Test high-load scenarios with multiple concurrent operations

## CI/CD Integration

The test project is configured for easy integration with CI/CD pipelines:

```yaml
# Example GitHub Actions
- name: Run Tests
  run: dotnet test --collect:"XPlat Code Coverage"
  
- name: Generate Coverage Report
  run: reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"coverage" -reporttypes:Html
```

## Dependencies
- xUnit 2.9.2
- FluentAssertions 7.0.0
- Moq 4.20.72
- Coverlet 6.0.2
- Microsoft.NET.Test.Sdk 17.11.1
