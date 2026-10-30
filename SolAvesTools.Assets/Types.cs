//---------------------------------------------------------------------------------------
// SPDX-FileCopyrightText: Copyright (C) 2026 Nenkai
// SPDX-License-Identifier: MIT
// SPDX-FileAttributionText: https://github.com/Nenkai/SolAvesTools
// SPDX-FileNotice: AI usage and consumption is prohibited. See AGENTS.md for policy.
//---------------------------------------------------------------------------------------

using SolAvesTools.Shared;

namespace SolAvesTools.Assets;

public record AssetContainerHash(byte[] Hash);
public record AssetDescription(AssetId AssetId, int DependencyListIndex, uint AssetInfoIndex);
public record AssetPathHash(XXHash3CustomHash AssetPath, AssetId AssetId);
public record AssetDataChunk(byte[] Sha256Hash, int AssetBlobIndex, ulong Unk4, ulong Size);
public record AssetDependencyList(List<uint> AssetIndices);
public record AssetLocaleRedirect(AssetId AssetId, ulong LocaleHash, AssetId TargetAssetId);
public record AssetInfo(XXHash3CustomHash AssetPathHash, XXHash3CustomHash AssetTypeHash, byte[] UnkHash, List<AssetSection> Sections);
public record AssetBlob(int AssetContainerIndex, AssetBlobFlags Flags, ulong FileOffset, ulong Size, ulong CompressedSize, byte[] Sha256Hash);

public record AssetSection(uint Type, uint Index, uint AssetBlobIndex, ulong Size, byte[] Hash);

// https://cedil.cesa.or.jp/cedil_sessions/view/3389
public record struct AssetId(ulong Value)
{
    public static implicit operator ulong(AssetId id) => id.Value;
    public static implicit operator AssetId(ulong id) => new() { Value = id };

    // They claim that bit 59 through 63 are special type bits
    // But I could only notice bit 63 as being only used in /Engine/ paths assets.
    public readonly bool IsEngineAsset => (Value >> 63) != 0;
}

[Flags]
public enum AssetBlobFlags : uint
{
    None = 0,
    Compressed = 1 << 0,
}

