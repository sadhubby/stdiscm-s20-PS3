using ProducerTest;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();
builder.Services.AddLogging(config =>
{
    config.ClearProviders();
    config.AddConsole();
});

var app = builder.Build();
// Ensure uploads folder exists in the project folder so mock persists files
var uploadsDir = Path.Combine(AppContext.BaseDirectory ?? Environment.CurrentDirectory, "uploads");
try { Directory.CreateDirectory(uploadsDir); } catch { }

app.MapGrpcService<MockVideoUploadService>();
app.MapGet("/", () => "Mock gRPC server for Producer testing (VideoUploadService active)");

// Serve uploaded files under /media
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadsDir),
    RequestPath = "/media",
    ServeUnknownFileTypes = true
});

// Simple index listing at /rabbit linking to files in uploads
app.MapGet("/rabbit", (HttpContext ctx) =>
{
    var files = Directory.Exists(uploadsDir)
        ? Directory.GetFiles(uploadsDir).Select(Path.GetFileName).OrderByDescending(n => n).ToArray()
        : Array.Empty<string>();

    var sb = new System.Text.StringBuilder();
    sb.Append("<html><head><meta charset='utf-8'><title>Mock Uploads</title></head><body>");
    sb.Append($"<h2>Uploads in {System.Net.WebUtility.HtmlEncode(uploadsDir)}</h2>");
    sb.Append("<ul>");
    foreach (var f in files)
    {
        var url = $"/media/{System.Uri.EscapeDataString(f)}";
        sb.Append($"<li><a href=\"{url}\">{System.Net.WebUtility.HtmlEncode(f)}</a></li>");
    }
    sb.Append("</ul></body></html>");

    ctx.Response.ContentType = "text/html; charset=utf-8";
    return ctx.Response.WriteAsync(sb.ToString());
});

var port = 5001; // Use HTTPS port
Console.WriteLine($"MockServer listening on https://localhost:{port}");
Console.WriteLine($"Configure Producer to connect here with: dotnet run --project Producer.csproj -- 1 ./test_videos https://localhost:{port}");
Console.WriteLine($"Uploads will be saved to: {uploadsDir}");
Console.WriteLine("(Ignore any certificate validation errors for testing)");

app.Urls.Add($"https://localhost:{port}");
await app.RunAsync();
