using System;
using System.Collections.Generic;
using UnityEngine;

namespace EvolutionShop
{
    /// <summary>Player-facing UI text keyed per screen, loaded from Resources/EvolutionShopUI/strings.en.json.</summary>
    public sealed class ShopUiStrings
    {
        [Serializable] sealed class Entry { public string key; public string text; }
        [Serializable] sealed class Table { public Entry[] entries; }

        public const string DefaultResource = "EvolutionShopUI/strings.en";
        public static readonly ShopUiStrings Empty = new ShopUiStrings(new Dictionary<string, string>());

        readonly Dictionary<string, string> texts;
        ShopUiStrings(Dictionary<string, string> texts) { this.texts = texts; }

        public int Count => texts.Count;
        public bool Has(string key) => key != null && texts.ContainsKey(key);
        /// <summary>The text for a key; a visible "[key]" placeholder when it is missing.</summary>
        public string Get(string key) => key != null && texts.TryGetValue(key, out var text) ? text : "[" + key + "]";

        /// <summary>The text for a key with {0}, {1} filled in; a table text that isn't a valid format shows as it is.</summary>
        public string Format(string key, params object[] args)
        {
            string text = Get(key);
            try { return string.Format(System.Globalization.CultureInfo.InvariantCulture, text, args); }
            catch (FormatException) { return text; }
        }

        /// <summary>Parses a string table. Rejects empty input, empty keys and duplicate keys.</summary>
        public static ShopUiStrings Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("The UI string table is empty.");
            var table = JsonUtility.FromJson<Table>(json);
            if (table == null || table.entries == null || table.entries.Length == 0)
                throw new FormatException("The UI string table has no entries.");
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var entry in table.entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.key)) throw new FormatException("A UI string entry has no key.");
                if (map.ContainsKey(entry.key)) throw new FormatException("Duplicate UI string key: " + entry.key);
                map.Add(entry.key, entry.text ?? "");
            }
            return new ShopUiStrings(map);
        }

        /// <summary>Loads the default English table. A missing or invalid table logs one error and returns Empty.</summary>
        public static ShopUiStrings LoadDefault()
        {
            var asset = Resources.Load<TextAsset>(DefaultResource);
            if (!asset) { Debug.LogError("UI string table missing: Resources/" + DefaultResource + ".json"); return Empty; }
            try { return Parse(asset.text); }
            catch (Exception problem) { Debug.LogError("UI string table invalid: " + problem.Message); return Empty; }
        }
    }
}
