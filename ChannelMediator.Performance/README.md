# ChannelMediator.Performance

Small console app to measure throughput of ChannelMediator.

Usage:

```bash
dotnet run --project ChannelMediator.Performance -- [messageCount] [messageSize] [concurrentSenders]
```

Defaults: 100000 messages, 256 bytes, 8 concurrent senders
