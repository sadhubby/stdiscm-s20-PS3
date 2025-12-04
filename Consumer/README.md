Consumer Implementation Guide

This document describes the Consumer-side implementation and expectations for the `VideoUploadService` and `VideoLibraryService` used by the Producer and GUI.

Overview

- The Consumer runs as an ASP.NET minimal app with gRPC endpoints and an HTTP static files endpoint `/media` which serves saved uploads.
- `VideoUploadService.UploadVideo` is a client-streaming RPC: Producers send one `VideoMetadata` message followed by binary `chunk` messages.
- The Consumer uses a leaky-bucket (`SemaphoreSlim`) to limit concurrently accepted uploads and an internal `Channel<UploadJob>` with background worker tasks to process finalization (checksum, rename/move to final location, create playback URL).

Key behaviors and implementation notes

- Acceptance vs rejection: Uploads are accepted only after the server successfully reserves a token from the leaky-bucket semaphore. If the bucket is full, the server must drain the incoming stream and return a response indicating the queue is full so the producer can retry.

- Temporary files: While streaming chunks, the server should write incoming bytes to a temporary file (e.g., `uploads/tmp_<guid>.part`). Only after processing and verifying the file should it be moved/renamed to the final filename (e.g., `<jobId>_<originalName>`).

- Job queue and workers: The server enqueues an `UploadJob` into a `Channel<UploadJob>` after the upload has been written to a temp file. A fixed number of worker tasks read from the channel and perform processing (checksum verification if provided, moving file to final name, generating playback URL). Workers must release the bucket token when the job is finished (or on error) so new uploads can be accepted.

- Playback URL: The server serves static files under `/media`. The final playback URL should be built from a configured `httpBaseUrl` and the final file name. Make sure to `Uri.EscapeDataString` the file name.

- Checksum validation: If the producer provides a `checksum_sha256` value in metadata, the worker should compute the SHA256 of the received file and compare it. If the checksum mismatches, the worker should still keep the file but mark the upload as having a checksum mismatch in the job completion message.

- Client-visible responses: When the upload finishes (worker processed job), the server should set the result (success flag, message, playback URL) in a `TaskCompletionSource` keyed by job id so the upload RPC can return meaningful results. On timeout, the upload RPC may return an "accepted, processing in background" response.

Testing notes

- When testing with the mock server (ProducerTest), the mock persists files to `ProducerTest/uploads` and simulates transient queue-full errors to exercise the Producer retry logic.

- To run the Consumer server locally, configure `appsettings.json` or use the defaults in `Program.cs` (`GrpcPort=5001`, `HttpPort=5000`). Ensure the `uploads` folder is writable by the running process.

Operational considerations

- If building the full solution causes Protobuf-generated class ambiguities, consider building only one project that generates the stubs, or adjust `Grpc.Tools` settings so only one project compiles server stubs while others use the generated shared assembly.

- For thumbnail generation the GUI relies on ffmpeg. Ensure the ffmpeg binary exists and the path is configured in `ConsumerGUI/VideoThumbnailer.cs` if running the GUI locally.

