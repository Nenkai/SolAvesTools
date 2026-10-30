//---------------------------------------------------------------------------------------
// SPDX-FileCopyrightText: Copyright (C) 2026 Nenkai
// SPDX-License-Identifier: MIT
// SPDX-FileAttributionText: https://github.com/Nenkai/SolAvesTools
// SPDX-FileNotice: AI usage and consumption is prohibited. See AGENTS.md for policy.
//---------------------------------------------------------------------------------------

using Microsoft.Extensions.Logging;

using NenTools.IO.Streams;

using System.Buffers.Binary;
using System.Drawing;
using System.Runtime.InteropServices;

namespace SolAvesTools.Files;

public class MovieContainer : IDisposable
{
    // "/Asset.Streaming/C93975A2MV.bin" is hardcoded into executable as a stack string

    public const uint HEADER_SIZE = OFFSET_TABLE_SIZE + MAIN_HEADER_SIZE;
    public const uint MAIN_HEADER_SIZE = 0x20;
    public const uint OFFSET_TABLE_SIZE = 0x1000;
    public static uint MAGIC => BinaryPrimitives.ReadUInt32LittleEndian("WBAC"u8);

    private readonly ILogger? _logger;

    private byte[] _table = null!;
    private ulong _state;
    private int _bitsLeft = 64;

    private Stream _stream;

    // Always ordered by hash for binary search
    public SortedDictionary<ulong, MovieEntry> Movies { get; } = [];

    public MovieContainer(ILoggerFactory? loggerFactory = null)
    {
        if (loggerFactory is not null)
            _logger = loggerFactory.CreateLogger<MovieContainer>();
    }

    public static MovieContainer Open(string file, ILoggerFactory? loggerFactory = null)
    {
        var fs = File.OpenRead(file);

        var reader = new MovieContainer(loggerFactory);
        if (!reader.OpenInternal(fs))
            throw new InvalidDataException("Failed to open movie reader.");

        return reader;
    }

    private bool OpenInternal(Stream stream)
    {
        if (stream.Length < HEADER_SIZE)
        {
            _logger?.LogError("Movie container stream is too small. Got 0x{:X}, required: 0x{:X}", stream.Length, HEADER_SIZE);
            return false;
        }

        _stream = stream;
        using var bs = new SmartBinaryStream(_stream, leaveOpen: true);

        _table = bs.ReadBytes((int)OFFSET_TABLE_SIZE);
        byte[] header = bs.ReadBytes((int)MAIN_HEADER_SIZE);
        if (_table.Length != OFFSET_TABLE_SIZE || header.Length != MAIN_HEADER_SIZE)
        {
            _logger?.LogError("Table or header length is too small.");
            return false;
        }

        // Init crypto
        _state = GetInitialState((ulong)stream.Length);
        var blocks = MemoryMarshal.Cast<byte, ulong>(header.AsSpan());
        for (int i = 0; i < 4; i++)
            blocks[i] ^= NextKey();

        // Header should be decrypted, read it
        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0x00));
        uint version = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0x04));
        ulong fileSize = BinaryPrimitives.ReadUInt64LittleEndian(header.AsSpan(0x08));
        uint numEntries = (uint)BinaryPrimitives.ReadUInt64LittleEndian(header.AsSpan(0x10));

        _logger?.LogInformation("Version: {}", version);
        _logger?.LogInformation("File Size: 0x{:X}", fileSize);
        _logger?.LogInformation("Num Movies: {}", numEntries);

        // Check it
        if (magic != MAGIC || version != 0x00010000) 
            return false;

        if (fileSize != (ulong)stream.Length) 
            return false;

        ulong[] hashes = bs.ReadUInt64s((int)numEntries);
        for (int i = 0; i < numEntries; i++)
            hashes[i] ^= NextKey();

        for (int i = 0; i < numEntries; i++)
        {
            ulong offset = bs.ReadUInt64() ^ NextKey();
            ulong size = bs.ReadUInt64() ^ NextKey();

            _logger?.LogInformation("- Movie #{}: 0x{:X}, Offset=0x{:X}, Size:{:X}", i, hashes[i], offset, size);

            Movies[hashes[i]] = new MovieEntry()
            {
                Hash = hashes[i],
                FileOffset = offset,
                Size = size,
            };
        }

        _logger?.LogInformation("Movie container OK.");
        return true;
    }

    public void ExtractAll(string outputDir)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDir, nameof(outputDir));

        Directory.CreateDirectory(outputDir);

        foreach (var entry in Movies.Values)
        {
            _logger?.LogInformation("Extracting 0x{:X}...", entry.Hash);

            byte[] data = GetEntry(entry.Hash);
            string outPath = Path.Combine(outputDir, $"{entry.Hash:X16}.webm");
            File.WriteAllBytes(outPath, data);
        }
    }

    public byte[] GetEntry(ulong hash)
    {
        var movie = Movies[hash];

        _state = GetInitialState(movie.Size);
        int count = (int)(_state & 7) + 8;
        _bitsLeft = 61;

        _stream.Position = (long)movie.FileOffset;
        byte[] data = new byte[movie.Size];
        _stream.ReadExactly(data);

        var key = new ulong[count];
        for (int i = 0; i < count; i++)
            key[i] = NextKey();

        int numBlocks = data.Length / 8;
        var blocks = MemoryMarshal.Cast<byte, ulong>(data.AsSpan(0, numBlocks * 8));
        for (int i = 0; i < blocks.Length; i++)
            blocks[i] ^= key[i % count];

        int rem = data.Length % 8;
        if (rem != 0)
        {
            ulong k = key[numBlocks % count];
            for (int j = 0; j < rem; j++)
                data[numBlocks * 8 + j] ^= (byte)(k >> (8 * j));
        }

        return data;
    }

    private ulong NextKey()
    {
        ulong state = 0;
        for (int i = 0; i < 8; i++)
            state = (state << 8) | _table[Read12Bits()];
        return state;
    }

    /// <summary>
    /// Reads 12 bits, every 64 bits (qword) is a new state
    /// </summary>
    /// <returns></returns>
    private int Read12Bits()
    {
        int value = 0, shift = 0, need = 12;
        while (need > 0)
        {
            if (_bitsLeft == 0)
            {
                _state = Step(_state);
                _bitsLeft = 64;
            }
            int take = Math.Min(need, _bitsLeft);
            ulong chunk = (_state >> (64 - _bitsLeft)) & ((1UL << take) - 1);
            value |= (int)chunk << shift;
            shift += take;
            need -= take;
            _bitsLeft -= take;
        }
        return value;
    }

    private static ulong Step(ulong x)
    {
        x ^= x << 17;
        x ^= x >> 47;
        x ^= x << 29;
        return x;
    }

    private static ulong GetInitialState(ulong fileSize)
    {
        ulong x = fileSize ^ 0x5E6334147752D96FUL;
        for (int i = 0; i < 64; i++) 
            x = Step(x);
        return x;
    }

    public void Dispose()
    {
        ((IDisposable)_stream).Dispose();
    }

    public class MovieEntry
    {
        public ulong Hash;
        public ulong FileOffset;
        public ulong Size;
    }
}
