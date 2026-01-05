using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using IO = System.IO;

namespace Corker.Infrastructure.AI;

public static class NativeLibraryConfigurator
{
    public static void Configure(string backend, ILogger logger)
    {
        try
        {
            IO.File.AppendAllText(IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug_log.txt"), $"Configuring backend: {backend}\n");
        }
        catch { }

        // Default to cpu if empty
        if (string.IsNullOrEmpty(backend)) backend = "cpu";

        string runtimeRoot = IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtimes", backend.ToLower());
        string? runtimePath = FindLibraryPath(runtimeRoot, "llama");
        string? ggmlPath = FindLibraryPath(runtimeRoot, "ggml");

        try
        {
            IO.File.AppendAllText(IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug_log.txt"), $"Runtime path: {runtimePath}\n");
        }
        catch { }

        logger.LogInformation("Attempting to load native library from {Path}", runtimePath ?? "(not found)");

        if (!string.IsNullOrWhiteSpace(ggmlPath) && IO.File.Exists(ggmlPath))
        {
            try
            {
                NativeLibrary.Load(ggmlPath);
                logger.LogInformation("Successfully loaded dependency: {Path}", ggmlPath);
                try
                {
                    IO.File.AppendAllText(IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug_log.txt"), $"Successfully loaded dependency: {ggmlPath}\n");
                }
                catch { }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load dependency: {Path}", ggmlPath);
            }
        }

        if (!string.IsNullOrWhiteSpace(runtimePath) && IO.File.Exists(runtimePath))
        {
            try
            {
                // Use LLamaSharp's configuration to specify the library path
                // WithLibrary requires both library path and llava path (can be null if not used)
                LLama.Native.NativeLibraryConfig.All.WithLibrary(runtimePath, null);

                // Also load explicitly just in case
                NativeLibrary.Load(runtimePath);

                logger.LogInformation("Successfully loaded native library for backend: {Backend}", backend);
                try
                {
                    IO.File.AppendAllText(IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug_log.txt"), $"Successfully loaded: {runtimePath}\n");
                }
                catch { }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load native library for backend: {Backend}", backend);
                try
                {
                    IO.File.AppendAllText(IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug_log.txt"), $"Failed to load: {ex}\n");
                }
                catch { }
            }
        }
        else
        {
            logger.LogWarning("Native library not found at {Path}. LLamaSharp might fail to initialize if not found elsewhere.", runtimePath ?? "(not found)");
            try
            {
                IO.File.AppendAllText(IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug_log.txt"), $"File not found: {runtimePath ?? "(not found)"}\n");
            }
            catch { }
        }
    }

    private static string? FindLibraryPath(string runtimeRoot, string baseName)
    {
        foreach (string fileName in GetCandidateFileNames(baseName))
        {
            string candidate = IO.Path.Combine(runtimeRoot, fileName);
            if (IO.File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string> GetCandidateFileNames(string baseName)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            yield return $"{baseName}.dll";
            yield break;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            yield return $"lib{baseName}.dylib";
            yield return $"{baseName}.dylib";
            yield break;
        }

        yield return $"lib{baseName}.so";
        yield return $"{baseName}.so";
    }
}
