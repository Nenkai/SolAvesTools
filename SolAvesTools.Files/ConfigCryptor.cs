//---------------------------------------------------------------------------------------
// SPDX-FileCopyrightText: Copyright (C) 2026 Nenkai
// SPDX-License-Identifier: MIT
// SPDX-FileAttributionText: https://github.com/Nenkai/SolAvesTools
// SPDX-FileNotice: AI usage and consumption is prohibited. See AGENTS.md for policy.
//---------------------------------------------------------------------------------------

using MessagePack;

using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SolAvesTools.Files;

public class ConfigCryptor
{
    private static readonly JsonSerializerOptions DefaultJsonSettings = new()
    {
        WriteIndented = true,
    };

    public static void DecryptConfig(string path, string outputPath)
    {
        var json = MessagePack.MessagePackSerializer.ConvertToJson(File.ReadAllBytes(path));
        JsonDocument doc = JsonDocument.Parse(json);

        var rootNode = JsonNode.Parse(json).AsObject();
        DecryptNode(rootNode);

        using var fs = File.Create(outputPath);
        JsonSerializer.Serialize(fs, rootNode, DefaultJsonSettings);

        void DecryptNode(JsonNode? node)
        {
            if (node.GetValueKind() == JsonValueKind.String)
            {
                byte[] stringBytes = Convert.FromBase64String(node.GetValue<string>());
                CryptoUtils.DecryptCommon(stringBytes);
                node.ReplaceWith(JsonValue.Create(Encoding.UTF8.GetString(stringBytes.AsSpan(0, stringBytes.Length - 1))));
            }

            if (node is JsonObject @object)
            {
                for (int i = @object.Count - 1; i >= 0; i--)
                {
                    var subNode = @object.ElementAt(i);
                    @object.Remove(subNode.Key);

                    byte[] stringBytes = Convert.FromBase64String(subNode.Key);
                    CryptoUtils.DecryptCommon(stringBytes);
                    @object.Add(Encoding.UTF8.GetString(stringBytes.AsSpan(0, stringBytes.Length - 1)), subNode.Value);

                    DecryptNode(subNode.Value);
                }
            }
            else if (node is JsonArray array)
            {
                for (int i = 0; i < array.Count; i++)
                {
                    DecryptNode(array[i]);
                }
            }
        }
    }

    // Can't use MessagePackSerializer.ConvertFromJson as it won't match
    public static void EncryptConfig(string jsonPath, string outputPath)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
        var buffer = new ArrayBufferWriter<byte>();
        var w = new MessagePackWriter(buffer);
        WriteValue(ref w, doc.RootElement);
        w.Flush();
        File.WriteAllBytes(outputPath, buffer.WrittenSpan.ToArray());
    }

    static void WriteEncrypted(ref MessagePackWriter w, string s)
        => w.Write(CryptoUtils.EncryptCommon(Encoding.UTF8.GetBytes(s), 0)); // bin, not str

    static void WriteValue(ref MessagePackWriter w, JsonElement e)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Object:
                w.WriteMapHeader(e.EnumerateObject().Count());
                foreach (var p in e.EnumerateObject())
                {
                    WriteEncrypted(ref w, p.Name);
                    WriteValue(ref w, p.Value);
                }
                break;
            case JsonValueKind.Array:
                w.WriteArrayHeader(e.GetArrayLength());
                foreach (var item in e.EnumerateArray()) WriteValue(ref w, item);
                break;
            case JsonValueKind.String: WriteEncrypted(ref w, e.GetString()!); break;
            case JsonValueKind.True: w.Write(true); break;
            case JsonValueKind.False: w.Write(false); break;
            case JsonValueKind.Null: w.WriteNil(); break;
            case JsonValueKind.Number:
                string raw = e.GetRawText();
                if (raw.AsSpan().IndexOfAny('.', 'e', 'E') >= 0)
                    w.Write(float.Parse(raw, CultureInfo.InvariantCulture));
                else if (e.TryGetInt64(out long l)) w.Write(l);
                else w.Write(e.GetUInt64());
                break;
        }
    }

    static string EncryptString(string value, byte seed = 0)
        => Convert.ToBase64String(CryptoUtils.EncryptCommon(Encoding.UTF8.GetBytes(value), seed));
}
