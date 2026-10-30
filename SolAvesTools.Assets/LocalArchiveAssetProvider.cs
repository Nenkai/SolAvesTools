//---------------------------------------------------------------------------------------
// SPDX-FileCopyrightText: Copyright (C) 2026 Nenkai
// SPDX-License-Identifier: MIT
// SPDX-FileAttributionText: https://github.com/Nenkai/SolAvesTools
// SPDX-FileNotice: AI usage and consumption is prohibited. See AGENTS.md for policy.
//---------------------------------------------------------------------------------------

using Microsoft.Extensions.Logging;

using SolAvesTools.Shared;

using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace SolAvesTools.Assets;

public class LocalArchiveAssetProvider : IDisposable
{
    private readonly ILoggerFactory? _loggerFactory;
    private readonly ILogger? _logger;

    public List<AssetCatalog> Catalogs { get; set; } = [];

    public LocalArchiveAssetProvider(ILoggerFactory? loggerFactory = null)
    {
        _loggerFactory = loggerFactory;
        if (loggerFactory is not null)
            _logger = loggerFactory.CreateLogger<LocalArchiveAssetProvider>();
    }

    public static LocalArchiveAssetProvider OpenDirectory(string path, ILoggerFactory? loggerFactory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, nameof(path));

        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException($"Directory not found: {path}");

        var localArchiveAssetProvider = new LocalArchiveAssetProvider(loggerFactory);
        localArchiveAssetProvider.OpenInternal(path);

        return localArchiveAssetProvider;
    }

    private void OpenInternal(string path)
    {
        var catalogFiles = Directory.GetFiles(path, "*.cat", SearchOption.TopDirectoryOnly);
        foreach (var catalogFilePath in catalogFiles)
        {
            _logger?.LogInformation("Opening catalog file {}", catalogFilePath);

            var catalog = AssetCatalog.Open(catalogFilePath, _loggerFactory);
            Catalogs.Add(catalog);
        }

        var dataContainers = Directory.GetFiles(path, "*.dat", SearchOption.TopDirectoryOnly);
        foreach (var dataContainerPath in dataContainers)
        {
            AssetDataContainerInfo containerHeaderInfo = AssetDataContainer.GetHeaderInformation(dataContainerPath);

            foreach (var catalog in Catalogs)
            {
                for (int i = 0; i < catalog.AssetContainerHashes.Count; i++)
                {
                    AssetContainerHash? hash = catalog.AssetContainerHashes[i];
                    if (!hash.Hash.SequenceEqual(containerHeaderInfo.Hash))
                        continue;

                    catalog.DataContainers[i] = AssetDataContainer.Open(dataContainerPath);

                    _logger?.LogInformation("Loaded container {}", dataContainerPath);

                }
            }
        }

        // Normally .dict reading goes here, but the game in release mode doesn't actually process it.

        int totalAssets = Catalogs.Sum(e => e.AssetDescriptions.Count);
        _logger?.LogInformation("Known hashes: {}/{}", AssetDictionary.Instance.Assets.Count, totalAssets);
    }

    /// <summary>
    /// Returns whether an asset exists by path.
    /// </summary>
    /// <param name="assetId"></param>
    /// <returns></returns>
    public bool AssetExists(string path)
    {
        XXHash3CustomHash hash = XXHash3Custom.HashPath(path);

        foreach (var catalog in Catalogs)
        {
            if (catalog.AssetPathHashes.ContainsKey(hash))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Returns whether an asset exists.
    /// </summary>
    /// <param name="assetId"></param>
    /// <returns></returns>
    public bool AssetExists(AssetId assetId)
    {
        return GetAssetSourceDescription(assetId) != null;
    }

    /// <summary>
    /// Returns an asset descriptor for the specified asset id.
    /// </summary>
    /// <param name="assetId"></param>
    /// <returns></returns>
    public AssetDescriptionSearchResult? GetAssetSourceDescription(AssetId assetId)
    {
        for (int i = 0; i < Catalogs.Count; i++)
        {
            AssetCatalog? catalog = Catalogs[i];
            if (catalog.AssetDescriptions.TryGetValue(assetId, out AssetDescription? desc))
                return new AssetDescriptionSearchResult(desc, catalog);
        }

        return null;
    }

    /// <summary>
    /// Finds an asset section info by asset id, section type and index.
    /// </summary>
    /// <param name="assetId"></param>
    /// <param name="type"></param>
    /// <param name="index"></param>
    /// <returns></returns>
    public AssetSectionInfoSearchResult? GetAssetSectionInfo(AssetId assetId, uint type, uint index)
    {
        AssetDescriptionSearchResult? searchResult = GetAssetSourceDescription(assetId);
        if (searchResult is null)
            return null;

        AssetCatalog catalog = searchResult.Catalog;
        AssetInfo assetInfo = catalog.AssetInfos[(int)searchResult.Description.AssetInfoIndex];
        foreach (AssetSection section in assetInfo.Sections)
        {
            if (section.Type == type && section.Index == index)
                return new AssetSectionInfoSearchResult(catalog.AssetBlobs[(int)section.AssetBlobIndex], catalog);
        }

        return null;
    }

    public void ExtractAllAssets(string outputDirectory, bool assetsAsZips = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory, nameof(outputDirectory));

        _logger?.LogInformation("Extracting all assets started.");
        _logger?.LogInformation("Output Directory: {}", outputDirectory);

        Directory.CreateDirectory(outputDirectory);

        foreach (AssetCatalog catalog in Catalogs)
        {
            foreach (AssetDescription assetDescription in catalog.AssetDescriptions.Values)
            {
                ExtractAsset(assetDescription.AssetId, outputDirectory, assetsAsZips);
            }
        }
    }

    public void ListFiles(string outputFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFile, nameof(outputFile));

        _logger?.LogInformation("Listing all assets started.");

        using var sw = new StreamWriter(outputFile);

        foreach (AssetCatalog catalog in Catalogs)
        {
            var orderedDescs = catalog.AssetDescriptions.OrderBy(e =>
            {
                var info = catalog.AssetInfos[(int)e.Value.AssetInfoIndex];
                var blob = catalog.AssetBlobs[(int)info.Sections[0].AssetBlobIndex];
                return blob.FileOffset;
            }).ToDictionary(e => e.Key, e => e.Value);

            foreach (var desc in orderedDescs.Values)
            {
                var info = catalog.AssetInfos[(int)desc.AssetInfoIndex];

                AssetDictionary.Instance.Assets.TryGetValue(info.AssetPathHash, out string? path);
                sw.WriteLine($"Id:{desc.AssetId.Value:X16}|Path:{info.AssetPathHash.Hash:X16}|Type:{info.AssetTypeHash.Hash:X16} ({(AssetTypes)info.AssetTypeHash.Hash})|{path}");
            }
        }
    }

    /// <summary>
    /// Extracts an asset to the specified directory.
    /// </summary>
    /// <param name="assetId"></param>
    /// <param name="outputDirectory"></param>
    /// <param name="locale"></param>
    /// <returns></returns>
    public bool ExtractAsset(AssetId assetId, string outputDirectory, bool asZip = false)
    {
        AssetInfoSearchResult? infoResult = GetAssetInfoForAssetWithLocale(assetId, 0);
        if (infoResult is null)
            return false;

        string assetOutputPath;
        if (AssetDictionary.Instance.Assets.TryGetValue(infoResult.Info.AssetPathHash, out string? assetPath))
        {
            _logger?.LogInformation("Extracting asset {} ({} sections)", assetPath, infoResult.Info.Sections.Count);
            if (assetPath.StartsWith('/'))
                assetPath = assetPath[1..];

            assetOutputPath = Path.Combine(outputDirectory, assetPath);
        }
        else
        {
            _logger?.LogInformation("Extracting unmapped asset {:X16} ({} sections)", assetId.Value, infoResult.Info.Sections.Count);
            string ext = AssetTypeToExtension.Extensions[(AssetTypes)(ulong)infoResult.Info.AssetTypeHash];
            assetOutputPath = Path.Combine(outputDirectory, ".unmapped", $"{ext}", $"{assetId.Value:X16}.{ext}");
        }

        Span<byte> typeBytes = stackalloc byte[4];
        if (asZip)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(assetOutputPath)!);

            using var fs = File.Create(assetOutputPath + ".zip");
            using ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Create);

            foreach (AssetSection section in infoResult.Info.Sections)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(typeBytes, section.Type);

                int len = typeBytes.IndexOf((byte)0);
                if (len < 0) len = 4;
                string type = Encoding.ASCII.GetString(typeBytes[..len]);

                var entry = zip.CreateEntry($"{type}.{section.Index}", CompressionLevel.NoCompression);
                using var entryStream = entry.Open();
                ExtractAssetSection(section, infoResult.Catalog, entryStream);
            }
        }
        else
        {
            Directory.CreateDirectory(assetOutputPath);

            foreach (AssetSection section in infoResult.Info.Sections)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(typeBytes, section.Type);

                int len = typeBytes.IndexOf((byte)0);
                if (len < 0) len = 4;
                string type = Encoding.ASCII.GetString(typeBytes[..len]);

                string outputPath = Path.Combine(assetOutputPath, $"{type}.{section.Index}");

                using var outputStream = File.OpenWrite(outputPath);
                ExtractAssetSection(section, infoResult.Catalog, outputStream);
            }
        }



        return true;
    }

    /// <summary>
    /// Extracts an asset's section to the specified stream.
    /// </summary>
    /// <param name="section"></param>
    /// <param name="outputStream"></param>
    /// <param name="catalog"></param>
    public void ExtractAssetSection(AssetSection section, AssetCatalog catalog, Stream outputStream)
    {
        AssetBlob blob = catalog.AssetBlobs[(int)section.AssetBlobIndex];

        AssetDataContainer assetContainer = catalog.DataContainers[blob.AssetContainerIndex];
        assetContainer.ExtractBlob(blob, outputStream);
    }

    /// <summary>
    /// Returns asset information for the specified asset and locale.
    /// </summary>
    /// <param name="assetId"></param>
    /// <param name="locale"></param>
    /// <returns></returns>
    private AssetInfoSearchResult? GetAssetInfoForAssetWithLocale(AssetId assetId, ulong locale)
    {
        AssetDescriptionSearchResult? description = GetAssetSourceDescription(assetId);
        if (description is null)
            return null;

        AssetCatalog catalog = description.Catalog;
        if (locale != 0)
        {
            int idx = catalog.LocaleRedirects.BinarySearch(new AssetLocaleRedirect(assetId, locale, 0), AssetLocaleRedirectComparer.Instance);
            if (idx < 0)
                return null;

            AssetId targetAssetId = catalog.LocaleRedirects[idx].TargetAssetId;
            if (!catalog.AssetDescriptions.TryGetValue(targetAssetId, out AssetDescription? desc))
                return null;

            return new AssetInfoSearchResult(catalog.AssetInfos[(int)desc.AssetInfoIndex], catalog);
        }
        else
        {
            return new AssetInfoSearchResult(catalog.AssetInfos[(int)description.Description.AssetInfoIndex], catalog);
        }

        
    }

    /// <summary>
    /// Returns the localized asset id for a specific asset with locale.
    /// </summary>
    /// <param name="assetId"></param>
    /// <param name="locale"></param>
    /// <returns></returns>
    public AssetId GetAssetIdForLocaleAsset(AssetId assetId, ulong locale)
    {
        AssetDescriptionSearchResult? searchResult = GetAssetSourceDescription(assetId);
        if (searchResult is null)
            return assetId;

        AssetCatalog catalog = searchResult.Catalog;
        int idx = catalog.LocaleRedirects.BinarySearch(new AssetLocaleRedirect(assetId, locale, 0), AssetLocaleRedirectComparer.Instance);
        if (idx < 0)
            return assetId;

        return catalog.LocaleRedirects[idx].TargetAssetId;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        foreach (var catalog in Catalogs)
            catalog?.Dispose();
    }

    public record AssetDescriptionSearchResult(AssetDescription Description, AssetCatalog Catalog);
    public record AssetInfoSearchResult(AssetInfo Info, AssetCatalog Catalog);
    public record AssetSectionInfoSearchResult(AssetBlob AssetBlob, AssetCatalog Catalog);
}
