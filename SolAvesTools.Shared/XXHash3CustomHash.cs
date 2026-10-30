//---------------------------------------------------------------------------------------
// SPDX-FileCopyrightText: Copyright (C) 2026 Nenkai
// SPDX-License-Identifier: MIT
// SPDX-FileAttributionText: https://github.com/Nenkai/SolAvesTools
// SPDX-FileNotice: AI usage and consumption is prohibited. See AGENTS.md for policy.
//---------------------------------------------------------------------------------------

namespace SolAvesTools.Shared;

public record struct XXHash3CustomHash(ulong Hash)
{
    public static implicit operator ulong(XXHash3CustomHash hash) => hash.Hash;
    public static implicit operator XXHash3CustomHash(ulong hash) => new() { Hash = hash };
}
