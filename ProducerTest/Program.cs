using ProducerTest;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();
builder.Services.AddLogging(config =>
{
    config.ClearProviders();
    config.AddConsole();
});

var app = builder.Build();

app.MapGrpcService<MockVideoUploadService>();
app.MapGet("/", () => "Mock gRPC server for Producer testing (VideoUploadService active)");

var port = 5001; // Use HTTPS port
Console.WriteLine($"MockServer listening on https://localhost:{port}");
Console.WriteLine($"Configure Producer to connect here with: dotnet run --project Producer.csproj -- 1 ./test_videos https://localhost:{port}");
Console.WriteLine("(Ignore any certificate validation errors for testing)");

app.Urls.Add($"https://localhost:{port}");
await app.RunAsync();
