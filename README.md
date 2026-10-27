# Audio Transcription Assessment (.NET 9 / C#)

A small, service-oriented transcription pipeline using Whisper.NET (local Whisper inference) and FFmpeg (audio normalization and chunking). It returns transcript text plus start/end timestamps for each segment.

## Engineering decisions

- **Local open-source inference:** Whisper.NET wraps `whisper.cpp`; no paid speech API is required.
- **Format normalization:** FFmpeg decodes formats it supports and converts audio to mono, 16 kHz, 16-bit PCM WAV.
- **Long recordings:** audio is split into configurable chunks (default 10 minutes); chunk-relative segment timestamps are offset back to the original recording timeline.
- **Separation of concerns:** `FfmpegAudioPreprocessor` handles audio conversion, `WhisperTranscriptionService` handles inference and result assembly, and the CLI handles arguments and JSON output.
- **Cleanup:** temporary chunk files are removed in a `finally` block.
- **Cancellation:** cancellation tokens are passed through preprocessing and transcription.

## Requirements

- .NET 9 SDK
- FFmpeg installed and available on `PATH`
- A Whisper GGML model file (for example `ggml-base.bin`)

## Get a model

Download a GGML model compatible with Whisper.NET from the Whisper.NET project's model repository / Hugging Face instructions. For a quick CPU demo, `base` is a reasonable starting point; `small` or larger models may improve quality but require more resources and are slower. Put the model in `Models/ggml-base.bin`, or pass its path with `--model`.

Model files are intentionally not committed to this source project because they are large binary artifacts.

## Run

```bash
dotnet restore
dotnet run -- "./samples/meeting.mp3" --model "./Models/ggml-base.bin" --language auto --output "./output/transcription.json"
```

For English only:

```bash
dotnet run -- "./samples/call.wav" --model "./Models/ggml-base.bin" --language en --output "./output/call.json"
```

To use 5-minute chunks:

```bash
dotnet run -- "./samples/long-recording.m4a" --model "./Models/ggml-base.bin" --chunk-seconds 300 --output "./output/long-recording.json"
```

Use `--help` for CLI usage.

## Example JSON response

```json
{
  "sourceFile": "meeting.mp3",
  "language": "auto",
  "model": "ggml-base.bin",
  "createdAtUtc": "2026-10-27T10:00:00+00:00",
  "durationSeconds": 12.7,
  "text": "Hello everyone. Let's start the meeting.",
  "segments": [
    { "index": 1, "startSeconds": 0.4, "endSeconds": 2.1, "text": "Hello everyone." },
    { "index": 2, "startSeconds": 2.3, "endSeconds": 5.8, "text": "Let's start the meeting." }
  ]
}
```

The example is illustrative, not an actual transcription.

## Notes and limitations

- FFmpeg must be installed separately. This implementation uses FFmpeg's segment muxer to create WAV chunks and assumes chunk boundaries follow the configured segment duration. Segment boundaries can vary slightly due to media/container behavior; for strict timing requirements, derive each chunk's actual start offset from FFmpeg metadata.
- The reported duration is based on the last recognized segment, so trailing silence is not included. If exact media duration is required, add an `ffprobe` duration read.
- Whisper segment timestamps are approximate speech segment timestamps, not word-level timestamps.
- For production APIs, add file-size and duration limits, content-type validation, authentication, rate limiting, structured logging, and queue/background processing for long files. Avoid logging audio or transcripts containing sensitive information.
- The assessment does not train a speech model. It uses an existing pretrained model for inference.
