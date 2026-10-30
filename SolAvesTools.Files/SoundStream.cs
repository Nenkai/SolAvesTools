//---------------------------------------------------------------------------------------
// SPDX-FileCopyrightText: Copyright (C) 2026 Nenkai
// SPDX-License-Identifier: MIT
// SPDX-FileAttributionText: https://github.com/Nenkai/SolAvesTools
// SPDX-FileNotice: AI usage and consumption is prohibited. See AGENTS.md for policy.
//---------------------------------------------------------------------------------------

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace SolAvesTools.Files;

public class SoundStream
{
    public static void DecryptSound(string path, string? outputPath = null)
    {
        // All opus soundstreams are expected to be encrypted
        byte[] dec = SoundStream.Decrypt(path);

        if (string.IsNullOrWhiteSpace(outputPath))
            File.WriteAllBytes(path, dec);
        else
            File.WriteAllBytes(outputPath, dec);
    }

    public static byte[] Decrypt(string file)
    {
        byte[] bytes = File.ReadAllBytes(file);

        // Hardcoded key in executable
        byte[] raw = MemoryMarshal.Cast<ulong, byte>([
            0x4669A6286A9CBEF7,
            0x3DAFF84505C33EB5,
            0x1FB659D6561D9D86
        ]).ToArray();

        byte[] mask = Encoding.ASCII.GetBytes("Ua#oK3P94vdxX,ft*k-mnjoO"); // Hardcoded mask
        for (int i = 0; i < raw.Length; i++)
            raw[i] ^= mask[i];

        using var aes = Aes.Create();
        aes.Key = raw;

        int len = bytes.Length & ~0xF;
        aes.DecryptEcb(bytes.AsSpan(0, len), bytes.AsSpan(0, len), PaddingMode.None);
        return bytes;
    }
}
