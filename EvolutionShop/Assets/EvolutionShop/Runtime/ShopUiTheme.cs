using System;
using System.Collections.Generic;
using UnityEngine;

namespace EvolutionShop
{
    // Original, code-authored UI art. This owner never reads or changes game state.
    internal sealed class ShopUiTheme : IDisposable
    {
        internal enum Symbol { Bloom, Flow, Spark, Mist, Sun, Moon, Coin, Star, Flask, Leaf }
        internal static readonly Color Paper = new Color(1f, .963f, .874f);
        internal static readonly Color Ink = new Color(.105f, .255f, .245f);
        internal static readonly Color Teal = new Color(.13f, .37f, .35f);
        internal static readonly Color Coral = new Color(.84f, .38f, .27f);
        internal static readonly Color Muted = new Color(.34f, .43f, .39f);
        internal static readonly Color Honey = new Color(.98f, .78f, .37f);
        internal readonly GUISkin Skin;
        internal readonly GUIStyle Card, DarkCard, Shadow, Inset, Accent, Button, PrimaryButton, DangerButton;
        internal readonly Texture2D FxDisc, FxRing;
        readonly List<Texture2D> textures = new List<Texture2D>();
        readonly Dictionary<Symbol, Texture2D> icons = new Dictionary<Symbol, Texture2D>();
        readonly Dictionary<GUIStyle, GUIStyle> compactSurfaces = new Dictionary<GUIStyle, GUIStyle>();

        internal ShopUiTheme(GUISkin source)
        {
            Skin = UnityEngine.Object.Instantiate(source);
            Skin.name = "Owned Evolution Shop UI skin";
            Skin.hideFlags = HideFlags.HideAndDontSave;
            Card = Surface(Paper, new Color(.75f, .69f, .53f, .72f));
            DarkCard = Surface(Teal, new Color(.08f, .24f, .23f));
            Shadow = Surface(new Color(.025f, .08f, .07f, .22f), Color.clear);
            Inset = Surface(new Color(.91f, .92f, .83f), new Color(.74f, .79f, .69f, .65f));
            Accent = Surface(Color.white, Color.clear);
            Button = MakeButton(source.button, new Color(.84f, .90f, .81f), new Color(.92f, .95f, .85f), new Color(.71f, .81f, .71f), Ink);
            PrimaryButton = MakeButton(source.button, Teal, new Color(.18f, .45f, .41f), new Color(.08f, .29f, .28f), Paper);
            DangerButton = MakeButton(source.button, new Color(.98f, .81f, .67f), new Color(1f, .87f, .73f), new Color(.90f, .66f, .53f), Ink);
            Skin.button = Button;
            Skin.label.normal.textColor = Ink;
            Skin.horizontalSlider = new GUIStyle(source.horizontalSlider);
            Skin.horizontalSlider.normal.background = Rounded(new Color(.64f, .74f, .66f), Color.clear);
            Skin.horizontalSlider.border = new RectOffset(4, 4, 0, 0);
            Skin.horizontalSliderThumb = new GUIStyle(source.horizontalSliderThumb);
            SetStates(Skin.horizontalSliderThumb, Rounded(Teal, Paper), Rounded(Coral, Paper), Rounded(Teal, Paper), Ink);
            Skin.horizontalSliderThumb.border = new RectOffset(0, 0, 0, 0);
            Skin.horizontalSliderThumb.fixedWidth = 16;
            Skin.horizontalSliderThumb.fixedHeight = 16;
            Skin.verticalScrollbar = new GUIStyle(source.verticalScrollbar);
            Skin.verticalScrollbar.normal.background = Rounded(new Color(.87f, .88f, .78f), Color.clear);
            Skin.verticalScrollbar.border = new RectOffset(8, 8, 8, 8);
            Skin.verticalScrollbarThumb = new GUIStyle(source.verticalScrollbarThumb);
            SetStates(Skin.verticalScrollbarThumb, Rounded(new Color(.42f, .62f, .52f), Color.clear), Rounded(Teal, Color.clear), Rounded(Teal, Color.clear), Ink);
            Skin.verticalScrollbarThumb.border = new RectOffset(8, 8, 8, 8);
            // Track/scrollbar anchors retain their native metrics; the slider thumb is
            // explicitly sized so it remains visible instead of collapsing into a sliver.
            Skin.verticalScrollbarUpButton = ScrollArrow(source.verticalScrollbarUpButton, true);
            Skin.verticalScrollbarDownButton = ScrollArrow(source.verticalScrollbarDownButton, false);
            foreach (Symbol symbol in Enum.GetValues(typeof(Symbol))) icons.Add(symbol, MakeIcon(symbol));
            FxDisc=MakeFxMask(false);FxRing=MakeFxMask(true);
        }

        /// <summary>The owned icon texture, shared with the UI Toolkit HUD (UX-03); null for an unknown symbol.</summary>
        internal Texture2D IconTexture(Symbol symbol) => icons.TryGetValue(symbol, out var texture) ? texture : null;

        Texture2D MakeFxMask(bool ring)
        {
            const int size=48;var texture=Own(new Texture2D(size,size,TextureFormat.RGBA32,false));
            texture.name=ring ? "Original dose ripple mask" : "Original dose bead mask";
            var pixels=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float radius=new Vector2((x+.5f)/size*2-1,(y+.5f)/size*2-1).magnitude;
                float alpha=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.8f,1,radius));
                if(ring)alpha*=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.55f,.72f,radius));
                pixels[y*size+x]=new Color(1,1,1,alpha);
            }
            texture.SetPixels(pixels);texture.Apply(false,true);return texture;
        }
        GUIStyle ScrollArrow(GUIStyle source, bool up)
        {
            var style = new GUIStyle(source);
            var arrow = Own(new Texture2D(32, 32, TextureFormat.RGBA32, false));
            var pixels = new Color[32 * 32];
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    Vector2 p = new Vector2((x + .5f) / 32f, (y + .5f) / 32f);
                    float py = up ? p.y : 1 - p.y;
                    pixels[y * 32 + x] = Triangle(new Vector2(p.x, py), new Vector2(.27f, .35f), new Vector2(.73f, .35f), new Vector2(.5f, .64f)) ? Teal : Color.clear;
                }
            arrow.SetPixels(pixels); arrow.Apply(false, true);
            SetStates(style, arrow, arrow, arrow, Ink);
            return style;
        }

        GUIStyle Surface(Color fill, Color outline)
        {
            var style = new GUIStyle { border = new RectOffset(14, 14, 14, 14) };
            style.normal.background = Rounded(fill, outline);
            var compact = new GUIStyle(style) { border = new RectOffset(8, 8, 8, 8) };
            // The smaller texture radius fits entirely within its eight-pixel corner slices.
            compact.normal.background = Rounded(fill, outline, 7.5f);
            compactSurfaces.Add(style, compact);
            return style;
        }

        GUIStyle MakeButton(GUIStyle source, Color normal, Color hover, Color active, Color text)
        {
            var style = new GUIStyle(source)
            {
                fontSize = 16, fontStyle = FontStyle.Bold, wordWrap = true,
                padding = new RectOffset(14, 14, 10, 10), border = new RectOffset(14, 14, 14, 14)
            };
            SetStates(style, Rounded(normal, new Color(.18f, .33f, .27f, .33f)),
                Rounded(hover, Honey), Rounded(active, Honey), text);
            return style;
        }

        static void SetStates(GUIStyle style, Texture2D normal, Texture2D hover, Texture2D active, Color text)
        {
            style.normal.background = style.onNormal.background = normal;
            style.hover.background = style.onHover.background = hover;
            style.active.background = style.onActive.background = active;
            style.focused.background = style.onFocused.background = hover;
            style.normal.textColor = style.hover.textColor = style.active.textColor = style.focused.textColor = text;
            style.onNormal.textColor = style.onHover.textColor = style.onActive.textColor = style.onFocused.textColor = text;
        }

        Texture2D Rounded(Color fill, Color outline, float radius = 11.5f)
        {
            const int size = 40;
            var texture = Own(new Texture2D(size, size, TextureFormat.RGBA32, false));
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float innerExtent = size / 2f - .5f - radius;
                    float dx = Mathf.Max(Mathf.Abs(x + .5f - size / 2f) - innerExtent, 0);
                    float dy = Mathf.Max(Mathf.Abs(y + .5f - size / 2f) - innerExtent, 0);
                    float distance = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                    Color color = outline.a > 0 ? Color.Lerp(fill, outline, Mathf.Clamp01(distance + 2)) : fill;
                    color.a *= Mathf.Clamp01(.5f - distance);
                    pixels[y * size + x] = color;
                }
            texture.SetPixels(pixels); texture.Apply(false, true);
            return texture;
        }

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

        internal void Draw(Rect rect, GUIStyle style, Color? tint = null)
        {
            if (Event.current.type != EventType.Repaint || rect.width <= 0 || rect.height <= 0) return;
            Color before = GUI.color;
            if (tint.HasValue) GUI.color = before * tint.Value;
            // IMGUI preserves nine-slice corner sizes even when their sum exceeds the
            // destination. Short cards use prebuilt compact corners; only tiny meters,
            // tabs and the reticle stretch. No style mutation/allocation occurs on repaint.
            if (rect.width < style.border.horizontal || rect.height < style.border.vertical)
            {
                if (rect.width >= 16 && rect.height >= 16 && compactSurfaces.TryGetValue(style, out var compact))
                    compact.Draw(rect, GUIContent.none, false, false, false, false);
                else
                    GUI.DrawTexture(rect, style.normal.background, ScaleMode.StretchToFill, true);
            }
            else
                style.Draw(rect, GUIContent.none, false, false, false, false);
            GUI.color = before;
        }
        internal void Icon(Rect rect, Symbol symbol) => GUI.DrawTexture(rect, icons[symbol], ScaleMode.ScaleToFit, true);
        public void Dispose()
        {
            foreach (var texture in textures) if (texture) UnityEngine.Object.Destroy(texture);
            textures.Clear(); icons.Clear(); compactSurfaces.Clear();
            if (Skin) UnityEngine.Object.Destroy(Skin);
        }
    }
}
