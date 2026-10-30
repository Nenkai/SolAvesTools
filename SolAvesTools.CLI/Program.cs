//---------------------------------------------------------------------------------------
// SPDX-FileCopyrightText: Copyright (C) 2026 Nenkai
// SPDX-License-Identifier: MIT
// SPDX-FileAttributionText: https://github.com/Nenkai/SolAvesTools
// SPDX-FileNotice: AI usage and consumption is prohibited. See AGENTS.md for policy.
//---------------------------------------------------------------------------------------

using Microsoft.Extensions.Logging;

using NLog.Extensions.Logging;

using SolAvesTools.Assets;
using SolAvesTools.Files;
using SolAvesTools.Shared;

using System.CommandLine;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;

namespace SolAvesTools.CLI;

public class Program
{
    private static ILoggerFactory _loggerFactory;
    private static ILogger _logger;

    public static Version? GetExecutableVersion() => Assembly.GetEntryAssembly()?.GetName().Version;

    static Program()
    {
        _loggerFactory = LoggerFactory.Create(builder => builder.AddNLog());
        _logger = _loggerFactory.CreateLogger<Program>();
    }

    static async Task<int> Main(string[] args)
    {
        Console.WriteLine("-----------------------------------------");
        Console.WriteLine($"- SolAvesTools {GetExecutableVersion():D3} by Nenkai -");
        Console.WriteLine("-----------------------------------------");
        Console.WriteLine("- https://github.com/Nenkai");
        Console.WriteLine("- https://twitter.com/Nenkaai");
        Console.WriteLine("-----------------------------------------");
        Console.WriteLine("");

        var extractAllCommand = new Command("extract-all", "Extracts all assets in the catalog/archive files.")
        {
            new Option<DirectoryInfo>("--input", aliases: ["-i"]) { Required = true, Description = "Input directory. This should be the directory containing .cat and .dat files." },
            new Option<string?>("--output", aliases: ["-o"]) { Description = "Output directory." },
            new Option<bool>("--zipped", aliases: ["-z"]) { Description = "Whether to extract assets as zips rather than folders." },
        };
        extractAllCommand.SetAction(ExtractAll);

        var listFilesCommand = new Command("list-files", "Lists all files in the catalog/archive files.")
        {
            new Option<DirectoryInfo>("--input", aliases: ["-i"]) { Required = true, Description = "Input directory. This should be the directory containing .cat and .dat files." },
            new Option<string?>("--output", aliases: ["-o"]) { Description = "Output directory." },
        };
        listFilesCommand.SetAction(ListFiles);

        var extractMoviesCommand = new Command("extract-movies", "Extracts all movies from the movie container (C93975A2MV.bin)")
        {
            new Option<FileInfo>("--input", aliases: ["-i"]) { Required = true, Description = "Input file (Normally C93975A2MV.bin)" },
            new Option<string?>("--output", aliases: ["-o"]) { Description = "Output directory." },
        };
        extractMoviesCommand.SetAction(ExtractMovies);

        var cryptConfigCommand = new Command("crypt-config", "En/decrypts a config file.")
        {
            new Option<FileInfo>("--input", aliases: ["-i"]) { Required = true, Description = "Input config.bin or config.json file." },
            new Option<string?>("--output", aliases: ["-o"]) { Description = "Output file path." },
            new Option<bool>("--force", aliases: ["-f"]) { Description = "Whether to not prompt if file already exists." },
        };
        cryptConfigCommand.SetAction(CryptConfig);

        var cryptStartupCommand = new Command("crypt-startup", "En/decrypts a startup.dat")
        {
            new Option<FileInfo>("--input", aliases: ["-i"]) { Required = true, Description = "Input startup.dat or config.json file." },
            new Option<string?>("--output", aliases: ["-o"]) { Description = "Output file path." },
            new Option<bool>("--force", aliases: ["-f"]) { Description = "Whether to not prompt if file already exists." },
        };
        cryptStartupCommand.SetAction(CryptStartup);

        var rootCommand = new RootCommand("SolAvesTools by Nenkai")
        {
            extractAllCommand,
            listFilesCommand,
            extractMoviesCommand,
            cryptConfigCommand,
            cryptStartupCommand,
        };
        
        return await rootCommand.Parse(args).InvokeAsync();
    }

    /// <summary>
    /// Extracts all files out of the specified catalog assets folder
    /// </summary>
    /// <param name="parseResult"></param>
    private static void ExtractAll(ParseResult parseResult)
    {
        DirectoryInfo inputDir = parseResult.GetValue<DirectoryInfo>("--input")!;
        bool assetsAsZip = parseResult.GetValue<bool>("--zipped")!;

        if (!inputDir.Exists)
        {
            _logger.LogError("Input directory must be specified.");
            return;
        }

        if (!GetOutputPath(parseResult, inputDir, out string? outputPath))
            return;

        var assetProvider = LocalArchiveAssetProvider.OpenDirectory(inputDir.FullName, _loggerFactory);
        assetProvider.ExtractAllAssets(outputPath, assetsAsZip);
    }

    /// <summary>
    /// Extracts all movies out of the specified .bin file
    /// </summary>
    /// <param name="parseResult"></param>
    private static void ExtractMovies(ParseResult parseResult)
    {
        FileInfo inputFile = parseResult.GetValue<FileInfo>("--input")!;
        if (!inputFile.Exists)
        {
            _logger.LogError("Input file '{}' does not exist.", inputFile.FullName);
            return;
        }

        string? outputPath = parseResult.GetValue<string>("--output");
        if (!string.IsNullOrEmpty(outputPath) && !Directory.Exists(outputPath))
        {
            _logger.LogError("Provided output path '{}' does not exist.", outputPath);
            return;
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            if (inputFile.DirectoryName is not null)
                outputPath = Path.Combine(inputFile.DirectoryName, $"{Path.GetFileNameWithoutExtension(inputFile.FullName)}_extracted");
            else
                outputPath = inputFile.FullName + "_extracted" ?? "extracted";
        }

        var assetProvider = MovieContainer.Open(inputFile.FullName!, _loggerFactory);
        assetProvider.ExtractAll(outputPath);
    }

    /// <summary>
    /// Lists all files in the specified catalog assets folder
    /// </summary>
    /// <param name="parseResult"></param>
    private static void ListFiles(ParseResult parseResult)
    {
        DirectoryInfo inputDir = parseResult.GetValue<DirectoryInfo>("--input")!;
        string? outputPath = parseResult.GetValue<string>("--output");
        
        if (!inputDir.Exists)
        {
            _logger.LogError("Input folder '{}' does not exist.", inputDir.FullName);
            return;
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            outputPath = Path.Combine(inputDir.FullName, $"{Path.GetFileNameWithoutExtension(inputDir.FullName)}_filelist.txt");
        }

        var assetProvider = LocalArchiveAssetProvider.OpenDirectory(inputDir.FullName, _loggerFactory);

        /*
        var bruteforcer = new AssetBruteforcer(assetProvider);
        bruteforcer.Bruteforce();
        */

        assetProvider.ListFiles(outputPath);

        _logger?.LogInformation("Done.");
    }

    /// <summary>
    /// Decrypts a config.bin file.
    /// </summary>
    /// <param name="parseResult"></param>
    private static void CryptConfig(ParseResult parseResult)
    {
        FileInfo inputFile = parseResult.GetValue<FileInfo>("--input")!;
        bool forceMode = parseResult.GetValue<bool>("--force")!;
        if (!inputFile.Exists)
        {
            _logger.LogError("Input file '{}' does not exist.", inputFile.FullName);
            return;
        }

        if (Path.GetExtension(inputFile.FullName) == ".bin")
        {
            _logger?.LogInformation("Decrypting {}...", inputFile.FullName);
            string outputPath = Path.ChangeExtension(inputFile.FullName, ".json");

            if (File.Exists(outputPath) && !forceMode)
            {
                Console.WriteLine($"File '{outputPath}' already exists. Overwrite? [Y/N]");
                if (Console.ReadKey().Key != ConsoleKey.Y)
                    return;

                Console.WriteLine();
            }

            ConfigCryptor.DecryptConfig(inputFile.FullName, outputPath);
        }
        else if (Path.GetExtension(inputFile.FullName) == ".json")
        {
            _logger?.LogInformation("Encrypting {}...", inputFile.FullName);

            string outputPath = Path.ChangeExtension(inputFile.FullName, ".bin");
            if (File.Exists(outputPath) && !forceMode)
            {
                Console.WriteLine($"File '{outputPath}' already exists. Overwrite? [Y/N]");
                if (Console.ReadKey().Key != ConsoleKey.Y)
                    return;

                Console.WriteLine();
            }

            ConfigCryptor.EncryptConfig(inputFile.FullName, outputPath);
        }
        else
        {
            _logger?.LogError("Unrecognized extension {}. Must be .bin or .json", Path.GetExtension(inputFile.FullName));
            return;
        }

        _logger?.LogInformation("Done.");
    }

    private static void CryptStartup(ParseResult parseResult)
    {
        FileInfo inputFile = parseResult.GetValue<FileInfo>("--input")!;
        bool forceMode = parseResult.GetValue<bool>("--force")!;
        if (!inputFile.Exists)
        {
            _logger.LogError("Input file '{}' does not exist.", inputFile.FullName);
            return;
        }

        if (Path.GetExtension(inputFile.FullName) == ".dat")
        {
            _logger?.LogInformation("Decrypting {}...", inputFile.FullName);
            string outputPath = Path.ChangeExtension(inputFile.FullName, ".json");

            if (File.Exists(outputPath) && !forceMode)
            {
                Console.WriteLine($"File '{outputPath}' already exists. Overwrite? [Y/N]");
                if (Console.ReadKey().Key != ConsoleKey.Y)
                    return;

                Console.WriteLine();
            }

            StartupCryptor.DecryptStartup(inputFile.FullName, outputPath);
        }
        else if (Path.GetExtension(inputFile.FullName) == ".json")
        {
            _logger?.LogInformation("Encrypting {}...", inputFile.FullName);

            string outputPath = Path.ChangeExtension(inputFile.FullName, ".dat");
            if (File.Exists(outputPath) && !forceMode)
            {
                Console.WriteLine($"File '{outputPath}' already exists. Overwrite? [Y/N]");
                if (Console.ReadKey().Key != ConsoleKey.Y)
                    return;

                Console.WriteLine();
            }

            StartupCryptor.EncryptStartup(inputFile.FullName, outputPath);
        }
        else
        {
            _logger?.LogError("Unrecognized extension {}. Must be .dat or .json", Path.GetExtension(inputFile.FullName));
            return;
        }

        _logger?.LogInformation("Done.");
    }

    private static bool GetOutputPath(ParseResult parseResult, DirectoryInfo inputDir, [NotNullWhen(true)] out string? outputPath)
    {
        outputPath = parseResult.GetValue<string>("--output");
        if (!string.IsNullOrEmpty(outputPath) && !Directory.Exists(outputPath))
        {
            _logger.LogError("Provided output path '{}' does not exist.", outputPath);
            return false;
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            var dirInfo = inputDir.FullName;
            if (dirInfo is not null)
                outputPath = Path.Combine(dirInfo, $"{Path.GetFileNameWithoutExtension(inputDir.FullName)}_extracted");
            else
                outputPath = inputDir.FullName ?? "extracted";
        }

        return true;
    }
}
