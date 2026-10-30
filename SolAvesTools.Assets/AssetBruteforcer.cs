using SolAvesTools.Shared;

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace SolAvesTools.Assets;

public partial class AssetBruteforcer
{
    public LocalArchiveAssetProvider _archiveAssetProvider;

    public AssetBruteforcer(LocalArchiveAssetProvider provider)
    {
        _archiveAssetProvider = provider;
    }

    public void Bruteforce()
    {
        //BruteforceFromStringsList();
        //BruteforceFromAssetWords();
    }

    private void BruteforceFromAssetWords()
    {
        HashSet<string> words = [];
        foreach (var asset in AssetDictionary.Instance.Assets)
        {
            string noExt = Path.GetFileNameWithoutExtension(asset.Value);
            string[] spl = noExt.Split("_");
            foreach (var range in noExt.AsSpan().Split("_"))
            {
                var part = noExt.AsSpan(0, range.End.Value);
                words.Add(part.ToString());
            }
        }

        var list = AssetDictionary.Instance.Assets.Values.ToList();
        var rockyou = File.ReadAllLines(@"D:\rockyou.txt");
        foreach (var i in list)
        {
            if (!i.Contains("normal"))
                continue;

            foreach (var word in rockyou)
            {
                RegisterFileIfValid(i.Replace("normal", word));
            }
        }

        foreach (var assetPath in list)
        {
            string ext = Path.GetExtension(assetPath);
            string noExt = Path.GetFileNameWithoutExtension(assetPath);
            string dir = Path.GetDirectoryName(assetPath)!;

            string newPath = Path.Combine(dir, noExt);
            foreach (var word in words)
            {
                RegisterFileIfValid($"{newPath}_{word}{ext}");
                RegisterFileIfValid($"{dir}/{word}{ext}");

                foreach (var part in noExt.AsSpan().Split("_"))
                {
                    if (part.Start.Value == 0 && part.End.Value == noExt.Length)
                        break;

                    var range = noExt.AsSpan(0, part.End.Value);
                    RegisterFileIfValid($"{dir}/{range}_{word}{ext}");
                }
            }
        }
    }

    private StreamReader BruteforceFromStringsList(string stringsListFile)
    {
        using var sr = File.OpenText(stringsListFile);
        while (!sr.EndOfStream)
        {
            string? line = sr.ReadLine();
            if (string.IsNullOrWhiteSpace(line))
                continue;


            for (int i = 0; i < 4; i++)
            {
                if (i > line.Length)
                    break;

                string fix = line.Substring(0, line.Length - i);
                RegisterFileIfValid($"/engine/material/{fix}.pmtrj");
                RegisterFileIfValid($"/game/ui/hud/{fix}/{fix}.pguij");
                RegisterFileIfValid($"/game/ui/menu/{fix}/{fix}.pguij");
                RegisterFileIfValid($"/game/ui/system/{fix}/{fix}.pguij");

                fix = line.Substring(i, line.Length - i);
                RegisterFileIfValid($"/engine/material/{fix}.pmtrj");
                RegisterFileIfValid($"/game/ui/hud/{fix}/{fix}.pguij");
                RegisterFileIfValid($"/game/ui/menu/{fix}/{fix}.pguij");
                RegisterFileIfValid($"/game/ui/system/{fix}/{fix}.pguij");
            }

            string[] spl = line.Split('_');
            if (spl.Length > 1)
            {
                foreach (var str in spl)
                {
                    RegisterFileIfValid($"/engine/material/{str}.pmtrj");
                    RegisterFileIfValid($"/game/ui/hud/{str}/{str}.pguij");
                    RegisterFileIfValid($"/game/ui/menu/{str}/{str}.pguij");
                    RegisterFileIfValid($"/game/ui/system/{str}/{str}.pguij");
                }

                if (line.StartsWith("Play", StringComparison.InvariantCulture))
                {
                    line = line.Substring(5);
                    spl = line.Split('_');

                    if (spl.Length > 3)
                    {
                        RegisterFileIfValid($"/game/sound/soundmain/audiosource/{spl[2]}/{line}.psasj");
                        if (line.Length > 1)
                            RegisterFileIfValid($"/game/sound/soundmain/audiosource/{spl[2]}/{line.Substring(1)}.psasj");
                        if (line.Length > 2)
                            RegisterFileIfValid($"/game/sound/soundmain/audiosource/{spl[2]}/{line.Substring(2)}.psasj");
                    }
                }
            }
        }

        return sr;
    }

    public bool RegisterFileIfValid(string file)
    {
        if (_archiveAssetProvider.AssetExists(file))
        {
            if (AssetDictionary.Instance.Assets.TryAdd(XXHash3Custom.HashPath(file), file))
            {
                Console.WriteLine($"Found {file.ToLowerInvariant()}");
            }

            return true;
        }

        return false;
    }

    [GeneratedRegex(@"(.)\1\1")]
    private static partial Regex MyRegex();

    private static void CleanStringsList(string stringsFile)
    {
        using var sr = File.OpenText(stringsFile);
        using var sw = new StreamWriter(stringsFile + "_cleaned.txt");
        while (!sr.EndOfStream)
        {
            string? line = sr.ReadLine();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            // Filter line with weird characters
            var filtered = line.Where(e => !char.IsAsciiLetterOrDigit(e) && e != '_' && e != ' ')
                .Distinct()
                .ToList();

            if (filtered.Count != 0)
                continue;

            if (line.Length < 3)
                continue;

            // Remove lines with triple same characters
            if (MyRegex().IsMatch(line))
                continue;

            sw.WriteLine(line);
        }
    }
}
