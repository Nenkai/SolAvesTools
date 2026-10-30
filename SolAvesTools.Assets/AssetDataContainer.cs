//---------------------------------------------------------------------------------------
// SPDX-FileCopyrightText: Copyright (C) 2026 Nenkai
// SPDX-License-Identifier: MIT
// SPDX-FileAttributionText: https://github.com/Nenkai/SolAvesTools
// SPDX-FileNotice: AI usage and consumption is prohibited. See AGENTS.md for policy.
//---------------------------------------------------------------------------------------

using NenTools.IO.Streams;
using NenTools.IO.Compression;

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

using Microsoft.Extensions.Logging;

namespace SolAvesTools.Assets;

public class AssetDataContainer : IDisposable
{
    public const int HEADER_SIZE = 0x38;
    public const int DATA_START_OFFSET = 0x40;

    public static readonly uint Magic = BinaryPrimitives.ReadUInt32LittleEndian("PDAT"u8); // Phoenix/Phx Data

    private Stream _stream;

    private readonly ILogger? _logger;

    /// <summary>
    /// Container version
    /// </summary>
    public uint Version { get; private set; }

    /// <summary>
    /// Container size
    /// </summary>
    public AssetDataContainerFlags Flags { get; private set; }

    /// <summary>
    /// Size of the container
    /// </summary>
    public ulong Size { get; private set; }

    /// <summary>
    /// SHA-256 hash of all the data in the container starting from 0x40
    /// </summary>
    public byte[] DataHash { get; private set; }

    private AssetDataContainer(ILoggerFactory? loggerFactory)
    {
        if (loggerFactory is not null)
            _logger = loggerFactory.CreateLogger<AssetDataContainer>();
    }

    public static AssetDataContainerInfo GetHeaderInformation(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, nameof(path));
        if (!File.Exists(path))
            throw new FileNotFoundException($"Asset data container file not found: {path}", path);

        using var fs = File.OpenRead(path);
        if (fs.Length < HEADER_SIZE)
            throw new InvalidDataException($"Asset data container header is too small (0x{fs.Length:X} bytes, expected at least {HEADER_SIZE})");
        using var bs = new SmartBinaryStream(fs);

        if (bs.ReadUInt32() != Magic)
            throw new InvalidDataException($"Invalid magic number for AssetDataContainer, expected 0x{Magic:X8}");

        var dataContainerInfo = new AssetDataContainerInfo(
            Version: bs.ReadUInt32(),
            Flags: (AssetDataContainerFlags)bs.ReadUInt64(),
            Size: bs.ReadUInt64(),
            Hash: bs.ReadBytes(SHA256.HashSizeInBytes)
        );

        return dataContainerInfo;
    }

    public static AssetDataContainer Open(string path, ILoggerFactory? loggerFactory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, nameof(path));

        if (!File.Exists(path))
            throw new FileNotFoundException($"Asset data container file not found: {path}", path);

        var fs = File.OpenRead(path);
        
        var assetDataContainer = new AssetDataContainer(loggerFactory);
        assetDataContainer.OpenInternal(fs);

        return assetDataContainer;
    }

    private void OpenInternal(Stream stream)
    {
        _stream = stream;

        using var bs = new SmartBinaryStream(_stream, leaveOpen: true);
        if (bs.ReadUInt32() != Magic)
            throw new InvalidDataException($"Invalid magic number for AssetDataContainer, expected 0x{Magic:X8}");

        uint version = bs.ReadUInt32();
        AssetDataContainerFlags flags = (AssetDataContainerFlags)bs.ReadUInt64();
        ulong dataSize = bs.ReadUInt64();
        byte[] hash = bs.ReadBytes(SHA256.HashSizeInBytes);

        if (dataSize != (ulong)stream.Length - DATA_START_OFFSET)
            throw new InvalidDataException($"Asset data container size mismatch, expected 0x{dataSize:X} bytes, got 0x{stream.Length:X} bytes");
        
        Version = version;
        Flags = flags;
        Size = dataSize;
        DataHash = hash;
    }

    public void ExtractBlob(AssetBlob blob, Stream outputStream)
    {
        ArgumentNullException.ThrowIfNull(blob, nameof(blob));
        ArgumentNullException.ThrowIfNull(outputStream, nameof(outputStream));

        _stream.Position = (long)blob.FileOffset;

        var compressionType = blob.Flags.HasFlag(AssetBlobFlags.Compressed) ?
            CompressionStreams.CompressionType.ZStd :
            CompressionStreams.CompressionType.None;

        using Stream compStream = CompressionStreams.New(_stream, compressionType);
        compStream.CopyStreamRange(outputStream, (uint)blob.CompressedSize);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        ((IDisposable)_stream).Dispose();
    }
}

public record AssetDataContainerInfo(uint Version, AssetDataContainerFlags Flags, ulong Size, byte[] Hash);

public enum AssetDataContainerFlags : ulong
{
    IsLoaded = 1,
}
