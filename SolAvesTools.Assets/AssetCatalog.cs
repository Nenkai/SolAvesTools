//---------------------------------------------------------------------------------------
// SPDX-FileCopyrightText: Copyright (C) 2026 Nenkai
// SPDX-License-Identifier: MIT
// SPDX-FileAttributionText: https://github.com/Nenkai/SolAvesTools
// SPDX-FileNotice: AI usage and consumption is prohibited. See AGENTS.md for policy.
//---------------------------------------------------------------------------------------

using Microsoft.Extensions.Logging;

using NenTools.IO.Streams;

using SolAvesTools.Shared;

using System;
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace SolAvesTools.Assets;

public class AssetCatalog : IDisposable
{
    public static readonly uint Magic = BinaryPrimitives.ReadUInt32LittleEndian("PARC"u8); // Phoenix Archive

    private readonly ILogger? _logger;

    public uint Version { get; set; }
    public List<AssetContainerHash> AssetContainerHashes { get; set; } = [];
    public Dictionary<AssetId, AssetDescription> AssetDescriptions { get; set; } = [];
    public Dictionary<XXHash3CustomHash, AssetId> AssetPathHashes { get; set; } = [];
    public List<AssetDataChunk> AssetDataChunks { get; set; } = [];
    public List<AssetDependencyList> DependencyLists { get; set; } = [];
    public List<AssetLocaleRedirect> LocaleRedirects { get; set; } = [];
    public List<AssetInfo> AssetInfos { get; set; } = [];
    public List<AssetBlob> AssetBlobs { get; set; } = [];

    internal AssetDataContainer[] DataContainers { get; set; } = null;

    public AssetCatalog(ILoggerFactory? loggerFactory = null)
    {
        if (loggerFactory is not null)
            _logger = loggerFactory.CreateLogger<LocalArchiveAssetProvider>();
    }

    public static AssetCatalog Open(string path, ILoggerFactory? loggerFactory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
            throw new FileNotFoundException($"Catalog file not found: {path}", path);

        using var fs = File.OpenRead(path);
        var catalog = new AssetCatalog(loggerFactory);
        catalog.OpenInternal(fs);
        return catalog;
    }


    private void OpenInternal(Stream stream)
    {
        SmartBinaryStream bs = new SmartBinaryStream(stream);

        ReadHeader(bs);
        ReadTableOfContents(bs);
    }

    private void ReadHeader(SmartBinaryStream bs)
    {
        uint magic = bs.ReadUInt32();
        if (magic != Magic)
            throw new InvalidDataException("Invalid magic number for AssetCatalog. Is this a catalog file?");

        Version = bs.ReadUInt32();
        ulong tocSize = bs.ReadUInt64();
        ulong field_0x10 = bs.ReadUInt64(); // 1000
        byte[] tocHash = bs.ReadBytes(SHA256.HashSizeInBytes);

        _logger?.LogInformation("- Version: {}", Version);
        _logger?.LogInformation("- SHA256: {}", Convert.ToHexString(tocHash));

        long tempPos = bs.Position;
        // Verify toc hash
        byte[] toc = bs.ReadBytes((int)tocSize);
        if (!SHA256.HashData(toc).AsSpan().SequenceEqual(tocHash))
            throw new InvalidDataException("TOC hash did not match. Is the catalog file corrupted?");

        bs.Position = tempPos;
    }

    private void ReadTableOfContents(SmartBinaryStream bs)
    {
        ReadAssetContainerHashes(bs);
        ReadAssetDescriptions(bs);
        ReadAssetPathHashes(bs);
        ReadDataChunks(bs);
        ReadDependencyLists(bs);
        ReadLocaleRedirect(bs);
        ReadAssetInfos(bs);
        ReadAssetBlobs(bs);
    }

    private void ReadAssetContainerHashes(SmartBinaryStream bs)
    {
        long tempPos = bs.Position;

        uint endOffset = bs.ReadUInt32();
        uint numAssetFiles = bs.ReadUInt32();
        uint field_0x08 = bs.ReadUInt32();
        for (int i = 0; i < numAssetFiles; i++)
        {
            byte[] hash = bs.ReadBytes(SHA256.HashSizeInBytes);
            AssetContainerHashes.Add(new AssetContainerHash(hash));
        }

        DataContainers = new AssetDataContainer[numAssetFiles];
        bs.Position = tempPos + endOffset;

        _logger?.LogInformation("- Asset Containers: {}", numAssetFiles);
    }

    private void ReadAssetDescriptions(SmartBinaryStream bs)
    {
        long tempPos = bs.Position;

        uint endOffset = bs.ReadUInt32();
        uint numDescs = bs.ReadUInt32();

        for (int i = 0; i < numDescs; i++)
        {
            AssetId assetId = bs.ReadUInt64();
            int dependencyListIndex = bs.ReadInt32();
            uint assetInfoIndex = bs.ReadUInt32();
            AssetDescriptions.Add(assetId, new AssetDescription(assetId, dependencyListIndex, assetInfoIndex));
        }

        bs.Position = tempPos + endOffset;

        _logger?.LogInformation("- Asset Descriptors: {}", numDescs);
    }

    private void ReadAssetPathHashes(SmartBinaryStream bs)
    {
        long tempPos = bs.Position;

        uint endOffset = bs.ReadUInt32();
        uint numEntries = bs.ReadUInt32();

        for (int i = 0; i < numEntries; i++)
        {
            XXHash3CustomHash assetPath = bs.ReadUInt64();
            AssetId assetId = bs.ReadUInt64();
            AssetPathHashes.Add(assetPath, assetId);
        }

        bs.Position = tempPos + endOffset;

        _logger?.LogInformation("- Asset Path Hashes: {}", numEntries);
    }

    private void ReadDataChunks(SmartBinaryStream bs)
    {
        long tempPos = bs.Position;

        uint endOffset = bs.ReadUInt32();
        uint numEntries = bs.ReadUInt32();

        for (int i = 0; i < numEntries; i++)
        {
            byte[] xxh3_128Hash = bs.ReadBytes(0x10);
            int assetBlobIndex = bs.ReadInt32();
            bs.ReadInt32(); // 0
            ulong unk4 = bs.ReadUInt64();
            ulong size = bs.ReadUInt64();
            AssetDataChunks.Add(new AssetDataChunk(xxh3_128Hash, assetBlobIndex, unk4, size));
        }

        bs.Position = tempPos + endOffset;
    }

    private void ReadDependencyLists(SmartBinaryStream bs)
    {
        long tempPos = bs.Position;

        uint endOffset = bs.ReadUInt32();
        uint numEntries = bs.ReadUInt32();

        long offsetListOffset = bs.Position;
        long entriesOffset = bs.Position + (numEntries * sizeof(uint));
        for (int i = 0; i < numEntries; i++)
        {
            bs.Position = offsetListOffset + (i * sizeof(uint));
            uint offset = bs.ReadUInt32();

            bs.Position = entriesOffset + offset;
            int numAssetIndices = bs.ReadInt32();
            List<uint> assetIndices = [];
            for (int j = 0; j < numAssetIndices; j++)
                assetIndices.Add(bs.ReadUInt32());

            DependencyLists.Add(new AssetDependencyList(assetIndices));
        }

        bs.Position = tempPos + endOffset;

        _logger?.LogInformation("- Dependency Lists: {}", numEntries);
    }

    private void ReadLocaleRedirect(SmartBinaryStream bs)
    {
        long tempPos = bs.Position;

        uint endOffset = bs.ReadUInt32();
        uint numEntries = bs.ReadUInt32();

        for (int i = 0; i < numEntries; i++)
        {
            AssetId sourceAssetId = bs.ReadUInt64();
            ulong targetHash = bs.ReadUInt64();
            AssetId targetAssetId = bs.ReadUInt64();

            LocaleRedirects.Add(new AssetLocaleRedirect(sourceAssetId, targetHash, targetAssetId));
        }

        bs.Position = tempPos + 0x08 + endOffset; // Only size of entries here

        _logger?.LogInformation("- Local Redirects: {}", numEntries);
    }

    private void ReadAssetInfos(SmartBinaryStream bs)
    {
        long tempPos = bs.Position;

        uint endOffset = bs.ReadUInt32();
        uint numEntries = bs.ReadUInt32();

        long offsetListOffset = bs.Position;
        long entriesOffset = bs.Position + (numEntries * sizeof(uint));
        for (int i = 0; i < numEntries; i++)
        {
            bs.Position = offsetListOffset + (i * sizeof(uint));
            uint offset = bs.ReadUInt32();

            bs.Position = entriesOffset + offset;
            int numSections = bs.ReadInt32();
            bs.ReadInt32();
            XXHash3CustomHash assetPathHash = bs.ReadUInt64();
            XXHash3CustomHash assetTypeHash = bs.ReadUInt64();
            byte[] unkHash = bs.ReadBytes(0x10);

            List<AssetSection> assetSections = [];
            for (int j = 0; j < numSections; j++)
            {
                uint typeFourCC = bs.ReadUInt32();
                uint index = bs.ReadUInt32();
                uint assetBlobIndex = (uint)bs.ReadInt64();
                ulong unkZero = bs.ReadUInt64();
                ulong size = bs.ReadUInt64();
                byte[] hash = bs.ReadBytes(0x10);
                assetSections.Add(new AssetSection(typeFourCC, index, assetBlobIndex, size, hash));
            }

            AssetInfos.Add(new AssetInfo(assetPathHash, assetTypeHash, unkHash, assetSections));
        }

        bs.Position = tempPos + endOffset;

        _logger?.LogInformation("- Asset Infos: {}", numEntries);
    }

    private void ReadAssetBlobs(SmartBinaryStream bs)
    {
        long tempPos = bs.Position;

        uint endOffset = bs.ReadUInt32();
        uint numEntries = bs.ReadUInt32();

        for (int i = 0; i < numEntries; i++)
        {
            int assetContainerIndex = bs.ReadInt32();
            AssetBlobFlags flags = (AssetBlobFlags)bs.ReadUInt32();
            ulong fileOffset = bs.ReadUInt64();
            ulong size = bs.ReadUInt64();
            ulong compressedSize = bs.ReadUInt64();
            byte[] sha256Hash = bs.ReadBytes(SHA256.HashSizeInBytes);

            AssetBlobs.Add(new AssetBlob(assetContainerIndex, flags, fileOffset, size, compressedSize, sha256Hash));
        }

        bs.Position = tempPos + endOffset;

        _logger?.LogInformation("- Asset Blobs: {}", numEntries);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (DataContainers is not null)
        {
            foreach (var container in DataContainers)
                container?.Dispose();
        }
    }
}

public class AssetLocaleRedirectComparer : IComparer<AssetLocaleRedirect>
{
    public static readonly AssetLocaleRedirectComparer Instance = new();

    public int Compare(AssetLocaleRedirect? x, AssetLocaleRedirect? y)
    {
        if (x is null && y is null) 
            return 0;

        if (x is null) 
            return -1;

        if (y is null) 
            return 1;

        int assetIdComparison = x.AssetId.Value.CompareTo(y.AssetId.Value);
        if (assetIdComparison != 0)
            return assetIdComparison;

        return x.LocaleHash.CompareTo(y.LocaleHash);
    }
}
