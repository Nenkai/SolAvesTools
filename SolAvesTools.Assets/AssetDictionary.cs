//---------------------------------------------------------------------------------------
// SPDX-FileCopyrightText: Copyright (C) 2026 Nenkai
// SPDX-License-Identifier: MIT
// SPDX-FileAttributionText: https://github.com/Nenkai/SolAvesTools
// SPDX-FileNotice: AI usage and consumption is prohibited. See AGENTS.md for policy.
//---------------------------------------------------------------------------------------

using SolAvesTools.Shared;

using System.Globalization;

namespace SolAvesTools.Assets;

public class AssetDictionary
{
    public static readonly AssetDictionary Instance = new();

    public Dictionary<XXHash3CustomHash, string> Assets { get; set; } = [];

    static AssetDictionary()
    {
        Instance.Initialize("hash_paths.txt");
    }

    public void Initialize(string path)
    {
        if (!File.Exists(path))
            return;

        using var sr = File.OpenText(path);
        while (!sr.EndOfStream)
        {
            var line = sr.ReadLine();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var parts = line.Split('|');
            if (parts.Length != 2)
                continue;

            Assets[XXHash3Custom.HashPath(parts[1])] = parts[1];
        }
    }

#if DEBUG
    public static void JsonToText(LocalArchiveAssetProvider assetProvider, string jsonPath, string outTxtPath)
    {
        var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllBytes(jsonPath));

        /*
        var cat = assetProvider.Catalogs.Select(e => e.AssetPathHashes);
        var full = new Dictionary<AssetId, XXHash3CustomHash>();
        foreach (var dict in cat)
        {
            foreach (var elem in dict)
                full.Add(elem.Value, elem.Key);
        }

        Dictionary<XXHash3CustomHash, string> fixedDict = [];
        foreach (var elem in full)
        {
            if (AssetDictionary.Instance.Assets.TryGetValue((ulong)elem.Key, out string? str))
            {
                fixedDict.Add(elem.Value, str);
            }
        }

        foreach (var current in AssetDictionary.Instance.Assets.Values)
        {
            if (!fixedDict.ContainsValue(current))
            {
                // database is missing one
            }
        }
        */

        var i = dict.OrderBy(e => e.Value).ToDictionary(k => k.Key, v => v.Value);
        File.WriteAllLines(outTxtPath, i.Select(e => $"{ulong.Parse(e.Key, NumberStyles.HexNumber):X16}|{e.Value}"));
    }
#endif
}
