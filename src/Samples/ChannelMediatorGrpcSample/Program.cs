using ProtoBuf.Grpc.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddChannelMediator(null, typeof(Program).Assembly);

// Register code-first gRPC (protobuf-net.Grpc)
builder.Services.AddCodeFirstGrpc();

var app = builder.Build();

// app.MapGrpcService();

await app.RunAsync();
