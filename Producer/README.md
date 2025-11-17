# Producer (Member 1)

This is the Producer console application. Each producer instance watches its own folder for video files and uploads them to the Consumer using gRPC client-streaming (`VideoUploadService.UploadVideo`).

Usage

From the `Producer` folder run:

```powershell
dotnet run --project Producer.csproj -- [p] [baseFolder] [serverAddress]
```

- `p` = number of producer instances (default: `1`)
- `baseFolder` = folder that contains per-producer subfolders `producer1`, `producer2`, ... (default: `./producer_inputs`)
- `serverAddress` = consumer server address e.g. `http://localhost:5000` (default shown)

Examples

1 producer, default folder and server:

```powershell
dotnet run --project Producer.csproj
```

Two producers, input base folder and server:

```powershell
dotnet run --project Producer.csproj -- 2 "C:\temp\producers" "http://192.168.1.50:5000"
```

Behavior

- Each producer watches its folder for files and uploads them in creation-time order.
- Files are uploaded using a metadata message followed by binary chunks (64 KB chunks by default).
- On successful upload the file is renamed with the suffix `.uploaded`.
- If the consumer reports the queue is full (or gRPC returns resource-exhausted/unavailable), the producer will retry with exponential backoff up to a configurable number of attempts.

Notes

- This project assumes the `Protos/streaming.proto` definitions are used by the Consumer server.
- Build and run the Consumer gRPC server before running producers.
