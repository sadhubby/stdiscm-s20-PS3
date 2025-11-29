# stdiscm-s20-pset3

PROJECT STRUCTURE:
stdiscm-PS3/
│
├── Protos/                     ← shared protocol definitions
│   ├── greet.proto
│   └── streaming.proto
│
├── Shared/                     ← shared logic (helper classes)
│   ├── Models/
│   │   └── VideoMetadata.cs
│   └── Utils/
│       └── FileHelper.cs
│
├── Producer/                   ← uploader client
│   └── Producer.csproj
│
├── Consumer/                   ← backend receiver (server)
│   └── Consumer.csproj
│
├── ConsumerGUI/                ← GUI client (your component)
│   └── ConsumerGUI.csproj
│
└── stdiscm-PS3.sln             ← the Visual Studio solution file


LAUNCH ORDER FOR TESTING:
1. Run Consumer (server) first → starts gRPC backend.
2. Run GUI → connects to Consumer and lists current uploads.
3. Run Producer(s) → uploads video files → Consumer saves them → GUI auto refresh shows new entries.


FOR GUI THUMBNAIL
- Go to https://www.gyan.dev/ffmpeg/builds/  
- download ffmpeg-release-essentials.zip
- update path, ex: string ffmpeg = @"D:\Users\user\Downloads\ffmpeg-8.0.1-essentials_build\ffmpeg-8.0.1-essentials_build\bin\ffmpeg.exe";
