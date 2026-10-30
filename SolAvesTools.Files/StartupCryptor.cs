//---------------------------------------------------------------------------------------
// SPDX-FileCopyrightText: Copyright (C) 2026 Nenkai
// SPDX-License-Identifier: MIT
// SPDX-FileAttributionText: https://github.com/Nenkai/SolAvesTools
// SPDX-FileNotice: AI usage and consumption is prohibited. See AGENTS.md for policy.
//---------------------------------------------------------------------------------------

using MessagePack;

using System.Buffers;
using System.Text.Json;

namespace SolAvesTools.Files;

public class StartupCryptor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public static void DecryptStartup(string path, string outputPath)
    {
        byte[] bytes = File.ReadAllBytes(path);
        CryptoUtils.DecryptCommon(bytes);

        var reader = new MessagePackReader(new ReadOnlySequence<byte>(bytes));
        var startupData = new StartupData
        {
            Version = reader.ReadInt32(),
            Hashes = MessagePackSerializer.Deserialize<Dictionary<string, string>>(ref reader),
            Providers = MessagePackSerializer.Deserialize<List<Provider>>(ref reader),
            Resources = MessagePackSerializer.Deserialize<List<Resource>>(ref reader),
            Unknown = reader.ReadInt32()
        };

        using var fs = File.OpenWrite(outputPath);
        JsonSerializer.Serialize(fs, startupData, JsonOptions);
    }

    public static void EncryptStartup(string path, string outputPath)
    {
        byte[] bytes = File.ReadAllBytes(path);

        StartupData startupData = JsonSerializer.Deserialize<StartupData>(bytes)
            ?? throw new InvalidDataException("Invalid startup JSON.");

        using var stream = new MemoryStream();

        ArrayBufferWriter<byte> bufWriter = new ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(bufWriter);

        writer.Write(startupData.Version);
        MessagePackSerializer.Serialize(ref writer, startupData.Hashes);
        MessagePackSerializer.Serialize(ref writer, startupData.Providers);
        MessagePackSerializer.Serialize(ref writer, startupData.Resources);
        writer.Write(startupData.Unknown);
        writer.Flush();

        bufWriter.GetSpan();

        Span<byte> msgPack = bufWriter.GetSpan();
        CryptoUtils.EncryptCommon(msgPack);
        File.WriteAllBytes(outputPath, msgPack);
    }
}

public partial class StartupData
{
    public int Version { get; set; }
    public Dictionary<string, string> Hashes { get; set; } = [];
    public List<Provider> Providers { get; set; } = [];
    public List<Resource> Resources { get; set; } = [];
    public int Unknown { get; set; }
}

[MessagePackObject]
public partial class Provider
{
    [Key("Label")]
    public string? Label { get; set; }

    [Key("Target")]
    public string? Target { get; set; }
}

[MessagePackObject]
public partial class Resource
{
    [Key("Label")]
    public string? Label { get; set; }

    [Key("Target")]
    public string? Target { get; set; }
}
