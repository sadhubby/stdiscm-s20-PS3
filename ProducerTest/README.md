# Producer Test Harness

This test project allows you to verify the **Producer** works independently without needing the full Consumer/GUI.

## What it does

- Runs a mock gRPC server that accepts `VideoUploadService.UploadVideo` streaming RPC calls from the Producer.
- Logs all uploads (metadata, chunk sizes, total bytes).
- Simulates queue-full responses every 3rd upload to test Producer retry logic.

## Quick test (Windows PowerShell)

**Terminal 1 - Start the mock server:**
```powershell
dotnet run --project "c:\Users\Rafael\OneDrive\Documents\Code\STDISCM\PS3\stdiscm-PS3\ProducerTest\ProducerTest.csproj"
```
Expected output:
```
MockServer listening on http://localhost:5000
Configure Producer to connect here with: dotnet run --project Producer.csproj -- 1 ./test_videos http://localhost:5000
```

**Terminal 2 - Create test video files:**
```powershell
# Create a test input folder
mkdir "C:\temp\test_videos\producer1" -Force

# Create a dummy video file (or copy a real .mp4)
# Example: create a 1MB dummy file
$null = New-Item "C:\temp\test_videos\producer1\test_video_1.mp4" -ItemType File -Force
(1..1000) | % { [byte[]]@(0..255) } | Set-Content "C:\temp\test_videos\producer1\test_video_1.mp4" -Encoding Byte
```

**Terminal 3 - Run the Producer:**
```powershell
dotnet run --project "c:\Users\Rafael\OneDrive\Documents\Code\STDISCM\PS3\stdiscm-PS3\Producer\Producer.csproj" -- 1 "C:\temp\test_videos" "http://localhost:5000"
```

## Expected behavior

1. **Producer** starts and watches `C:\temp\test_videos\producer1` for files.
2. **Producer** finds `test_video_1.mp4`, connects to mock server, and streams upload.
3. **MockServer** logs:
   - Upload started with metadata (filename, upload ID, checksum, etc.)
   - Chunk arrivals and total bytes
   - Queue-full simulation (every 3rd upload)
4. **Producer** on queue-full: backs off with exponential delay and retries (up to 5 attempts).
5. File renamed to `.uploaded` on success.

## Testing retry logic

To see retries in action:
- Upload 3+ files — the 3rd will trigger queue-full, and the Producer will backoff and retry.
- Check Producer console logs for messages like:
  ```
  Queue full, retrying after 2s (attempt 1/5)
  ```

## To add more producers

```powershell
# Create additional producer folders
mkdir "C:\temp\test_videos\producer2" -Force
mkdir "C:\temp\test_videos\producer3" -Force

# Add test files to each

# Run Producer with 3 instances
dotnet run --project Producer.csproj -- 3 "C:\temp\test_videos" "http://localhost:5000"
```

Once verified, you can:
- Implement the real Consumer `VideoUploadService` in `Services/VideoUploadService.cs`.
- Map it in the main `Program.cs`.
- Build and run the Consumer server with the same port.
- Replace the mock server with the real one.
