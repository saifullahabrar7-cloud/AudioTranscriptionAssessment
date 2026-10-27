using System.Text.Json;
using AudioTranscriptionAssessment.Services;
using Microsoft.Extensions.DependencyInjection;

static string? GetOption(string[] args, string option)
{
    var index = Array.FindIndex(args, x => string.Equals(x, option, StringComparison.OrdinalIgnoreCase));
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

if (args.Length == 0 || args[0] is "--help" or "-h")
{
    Console.WriteLine("AudioTranscriptionAssessment");
    Console.WriteLine("Usage: dotnet run -- <audio-file> --model <path-to-ggml-model.bin> [--language auto|en|ur] [--output result.json] [--chunk-seconds 600]");
    Console.WriteLine("Requires FFmpeg on PATH. Supported input formats are those FFmpeg can decode.");
    return;
}

var audioPath = args[0];
var modelPath = GetOption(args, "--model") ?? Path.Combine(AppContext.BaseDirectory, "Models", "ggml-base.bin");
var language = GetOption(args, "--language") ?? "auto";
var outputPath = GetOption(args, "--output");
var chunkSecondsText = GetOption(args, "--chunk-seconds");
var chunkSeconds = int.TryParse(chunkSecondsText, out var parsed) ? parsed : 600;

if (!File.Exists(audioPath))
{
    Console.Error.WriteLine($"Audio file not found: {audioPath}");
    Environment.ExitCode = 2;
    return;
}

try
{
    var services = new ServiceCollection();
    services.AddSingleton<FfmpegAudioPreprocessor>();
    services.AddSingleton(sp => new WhisperTranscriptionService(
        sp.GetRequiredService<FfmpegAudioPreprocessor>(), modelPath, chunkSeconds));

    using var provider = services.BuildServiceProvider();
    var service = provider.GetRequiredService<WhisperTranscriptionService>();

    Console.WriteLine($"Transcribing: {Path.GetFileName(audioPath)}");
    Console.WriteLine("First run may take longer while the model is loaded.");

    var result = await service.TranscribeAsync(audioPath, language);
    var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });

    if (string.IsNullOrWhiteSpace(outputPath))
        Console.WriteLine(json);
    else
    {
        var fullOutputPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath)!);
        await File.WriteAllTextAsync(fullOutputPath, json);
        Console.WriteLine($"Transcription saved to: {fullOutputPath}");
    }
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Transcription was cancelled.");
    Environment.ExitCode = 130;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Transcription failed: {ex.Message}");
    Environment.ExitCode = 1;
}
