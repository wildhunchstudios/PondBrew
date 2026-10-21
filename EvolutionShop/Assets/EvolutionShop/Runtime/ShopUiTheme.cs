using System;
using System.Collections.Generic;
using UnityEngine;

namespace EvolutionShop
{
    // Original, code-authored UI palette and icons (coin, star, sun and moon, the four cultures, flask and leaf). Since UX-09 the
    // UI Toolkit root owns the one instance (ShopUiRoot.Icons) and disposes it; this owner never reads or changes game state.
    internal sealed class ShopUiTheme : IDisposable
    {
        internal enum Symbol { Bloom, Flow, Spark, Mist, Sun, Moon, Coin, Star, Flask, Leaf }
        internal static readonly Color Paper = new Color(1f, .963f, .874f);
        internal static readonly Color Ink = new Color(.105f, .255f, .245f);
        internal static readonly Color Teal = new Color(.13f, .37f, .35f);
        internal static readonly Color Coral = new Color(.84f, .38f, .27f);
        internal static readonly Color Muted = new Color(.34f, .43f, .39f);
        internal static readonly Color Honey = new Color(.98f, .78f, .37f);
        readonly List<Texture2D> textures = new List<Texture2D>();
        readonly Dictionary<Symbol, Texture2D> icons = new Dictionary<Symbol, Texture2D>();

        internal ShopUiTheme()
        {
            foreach (Symbol symbol in Enum.GetValues(typeof(Symbol))) icons.Add(symbol, MakeIcon(symbol));
        }

        /// <summary>The owned icon texture the UI Toolkit draws (HUD, cards, notebook); null for an unknown symbol.</summary>
        internal Texture2D IconTexture(Symbol symbol) => icons.TryGetValue(symbol, out var texture) ? texture : null;

        Texture2D Own(Texture2D texture)
        {
            texture.name = "Owned shop UI texture " + textures.Count;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.hideFlags = HideFlags.HideAndDontSave;
            textures.Add(texture);
            return texture;
        }

        Texture2D MakeIcon(Symbol symbol)
        {
            const int size = 64;
            var texture = Own(new Texture2D(size, size, TextureFormat.RGBA32, false));
            texture.name = "Owned shop UI icon / " + symbol;
            var pixels = new Color[size * size];
            Color color = SymbolColor(symbol);
            // Four subpixel samples keep the silhouettes clean at 18–32 logical pixels.
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Color total = Color.clear;
                    for (int sy = 0; sy < 2; sy++)
                        for (int sx = 0; sx < 2; sx++)
                        {
                            Vector2 p = new Vector2((x + (sx + .5f) / 2) / size, (y + (sy + .5f) / 2) / size);
                            if (!Shape(symbol, p)) continue;
                            bool edge = !Shape(symbol, p + Vector2.right * .035f) || !Shape(symbol, p - Vector2.right * .035f) ||
                                !Shape(symbol, p + Vector2.up * .035f) || !Shape(symbol, p - Vector2.up * .035f);
                            Color sample = edge ? Ink : color;
                            if (!edge && symbol == Symbol.Bloom && Circle(p, .5f, .5f, .105f)) sample = Honey;
                            if (!edge && symbol == Symbol.Coin && Mathf.Abs(p.x - .5f) < .027f && p.y > .31f && p.y < .69f) sample = Ink;
                            if (!edge && symbol == Symbol.Flask && p.y < .4f) sample = new Color(.48f, .73f, .57f);
                            if (!edge && Circle(p, .39f, .64f, .055f) && symbol != Symbol.Moon && symbol != Symbol.Spark) sample = Paper;
                            total += sample * .25f;
                        }
                    if (total.a > 0) { total.r /= total.a; total.g /= total.a; total.b /= total.a; }
                    pixels[y * size + x] = total;
                }
            texture.SetPixels(pixels); texture.Apply(false, true);
            return texture;
        }

        internal static Color SymbolColor(Symbol symbol)
        {
            switch (symbol)
            {
                case Symbol.Bloom: return new Color(.96f, .48f, .35f);
                case Symbol.Flow: return new Color(.35f, .70f, .79f);
                case Symbol.Spark: case Symbol.Coin: case Symbol.Sun: case Symbol.Star: return Honey;
                case Symbol.Mist: case Symbol.Moon: return new Color(.75f, .66f, .85f);
                default: return new Color(.54f, .75f, .56f);
            }
        }

        static bool Shape(Symbol symbol, Vector2 p)
        {
            switch (symbol)
            {
                case Symbol.Bloom:
                    for (int i = 0; i < 5; i++)
                    {
                        float angle = i * Mathf.PI * 2 / 5 + Mathf.PI / 2;
                        if (Circle(p, .5f + Mathf.Cos(angle) * .19f, .5f + Mathf.Sin(angle) * .19f, .18f)) return true;
                    }
                    return Circle(p, .5f, .5f, .22f);
                case Symbol.Flow:
                    return Circle(p, .5f, .36f, .25f) || Triangle(p, new Vector2(.25f, .43f), new Vector2(.75f, .43f), new Vector2(.5f, .9f));
                case Symbol.Spark:
                    return Triangle(p, new Vector2(.59f, .92f), new Vector2(.23f, .43f), new Vector2(.62f, .43f)) ||
                        Triangle(p, new Vector2(.4f, .08f), new Vector2(.78f, .57f), new Vector2(.39f, .57f));
                case Symbol.Mist:
                    return Circle(p, .31f, .53f, .18f) || Circle(p, .51f, .65f, .22f) || Circle(p, .71f, .53f, .17f) ||
                        (p.x > .3f && p.x < .72f && p.y > .36f && p.y < .61f) || Circle(p, .38f, .2f, .047f) || Circle(p, .64f, .17f, .047f);
                case Symbol.Moon: return Circle(p, .49f, .51f, .35f) && !Circle(p, .68f, .67f, .29f);
                case Symbol.Sun:
                    if (Circle(p, .5f, .5f, .25f)) return true;
                    for (int i = 0; i < 8; i++)
                    {
                        float angle = i * Mathf.PI / 4;
                        if (Circle(p, .5f + Mathf.Cos(angle) * .35f, .5f + Mathf.Sin(angle) * .35f, .05f)) return true;
                    }
                    return false;
                case Symbol.Coin: return Circle(p, .5f, .5f, .36f);
                case Symbol.Star:
                    Vector2 d = p - new Vector2(.5f, .5f);
                    float angleFromTop = Mathf.Atan2(d.x, d.y);
                    return d.magnitude < .285f + .1f * Mathf.Cos(angleFromTop * 5);
                case Symbol.Flask:
                    return (p.x > .39f && p.x < .61f && p.y > .52f && p.y < .87f) ||
                        (p.x > .32f && p.x < .68f && p.y > .8f && p.y < .9f) ||
                        Triangle(p, new Vector2(.16f, .14f), new Vector2(.84f, .14f), new Vector2(.5f, .69f));
                case Symbol.Leaf:
                    return ((p.x - .48f) * (p.x - .48f) / .07f + (p.y - .58f) * (p.y - .58f) / .105f < 1) ||
                        (p.x > .46f && p.x < .53f && p.y > .1f && p.y < .55f);
                default: return false;
            }
        }

        static bool Circle(Vector2 p, float x, float y, float radius) => (p - new Vector2(x, y)).sqrMagnitude < radius * radius;
        static float Side(Vector2 a, Vector2 b, Vector2 p) => (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
        static bool Triangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Side(a, b, p), d2 = Side(b, c, p), d3 = Side(c, a, p);
            return !((d1 < 0 || d2 < 0 || d3 < 0) && (d1 > 0 || d2 > 0 || d3 > 0));
        }

        public void Dispose()
        {
            // Destroy in play; edit-mode callers (EditMode tests) need DestroyImmediate.
            foreach (var texture in textures) if (texture) { if (Application.isPlaying) UnityEngine.Object.Destroy(texture); else UnityEngine.Object.DestroyImmediate(texture); }
            textures.Clear(); icons.Clear();
        }
    }
}
