using UnityEngine;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    /// <summary>
    /// A crew member's portrait on the crew tab (UX.6, after the approved crew mockup): Pip in a coral cap, Nori in a
    /// teal beanie with a honey pom, Moss in a lab coat and goggles, each on their own pale disc. Drawn, not an image.
    /// </summary>
    public sealed class CrewPortrait : VisualElement
    {
        public const string ClassName = "es-plan-portrait";
        static readonly Color Ink = new Color(0.105f, 0.255f, 0.245f);
        CrewRole role;
        bool dimmed;

        public CrewRole Role => role;

        public CrewPortrait(CrewRole role)
        {
            this.role = role;
            AddToClassList(ClassName);
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        /// <summary>Off duty and not hired portraits are drawn paler, so the cards read at a glance; the status chip says why.</summary>
        public void SetDimmed(bool value)
        {
            if (value == dimmed) return;
            dimmed = value;
            MarkDirtyRepaint();
        }

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);

        Color Tone(Color c) => dimmed ? Color.Lerp(c, new Color(0.93f, 0.91f, 0.85f), 0.45f) : c;

        static void Circle(Painter2D p, Vector2 at, float radius)
        {
            p.BeginPath(); p.Arc(at, radius, Angle.Degrees(0), Angle.Degrees(360)); p.ClosePath();
        }

        void Draw(MeshGenerationContext context)
        {
            Rect r = contentRect;
            if (r.width <= 0 || r.height <= 0) return;
            float s = Mathf.Min(r.width, r.height) / 84f;
            Vector2 o = new Vector2(r.center.x - 42 * s, r.center.y - 42 * s);
            Vector2 P(float x, float y) => o + new Vector2(x * s, y * s);
            var p = context.painter2D;
            Color disc = role == CrewRole.Cashier ? Hex(0xF7D9C9) : role == CrewRole.Stocker ? Hex(0xD3E9EC) : Hex(0xE2EBD5);
            Color shirt = role == CrewRole.Cashier ? Hex(0xD66145) : role == CrewRole.Stocker ? Hex(0x2E7F95) : Hex(0xF4F1E6);
            Color skin = role == CrewRole.Cashier ? Hex(0xC68A60) : role == CrewRole.Stocker ? Hex(0xE0B48E) : Hex(0x9C6B4A);
            p.fillColor = Tone(disc); Circle(p, P(42, 42), 40 * s); p.Fill();
            // Shoulders.
            p.fillColor = Tone(shirt);
            p.BeginPath(); p.MoveTo(P(14, 82)); p.BezierCurveTo(P(16, 64), P(28, 58), P(42, 58)); p.BezierCurveTo(P(56, 58), P(68, 64), P(70, 82)); p.ClosePath(); p.Fill();
            if (role == CrewRole.Technician)
            {
                p.fillColor = Tone(Hex(0x7FB98A));
                p.BeginPath(); p.MoveTo(P(38, 58)); p.LineTo(P(42, 70)); p.LineTo(P(46, 58)); p.ClosePath(); p.Fill();
            }
            p.fillColor = Tone(skin); Circle(p, P(42, 38), 16 * s); p.Fill();
            if (role == CrewRole.Cashier)
            {
                // A coral cap with its brim to one side.
                p.fillColor = Tone(Hex(0xD66145));
                p.BeginPath(); p.MoveTo(P(24, 32)); p.BezierCurveTo(P(26, 18), P(58, 18), P(60, 32)); p.ClosePath(); p.Fill();
                p.fillColor = Tone(Hex(0xB24A33));
                p.BeginPath(); p.MoveTo(P(42, 27)); p.LineTo(P(66, 27)); p.LineTo(P(66, 31)); p.LineTo(P(42, 31)); p.ClosePath(); p.Fill();
            }
            else if (role == CrewRole.Stocker)
            {
                p.fillColor = Tone(Hex(0x215E59));
                p.BeginPath(); p.MoveTo(P(25, 34)); p.BezierCurveTo(P(25, 16), P(59, 16), P(59, 34)); p.ClosePath(); p.Fill();
                p.fillColor = Tone(Hex(0xFAC75E)); Circle(p, P(42, 17), 4 * s); p.Fill();
            }
            else
            {
                p.fillColor = Tone(Hex(0x3B2A22));
                p.BeginPath(); p.MoveTo(P(26, 30)); p.BezierCurveTo(P(28, 20), P(56, 20), P(58, 30)); p.BezierCurveTo(P(52, 26), P(32, 26), P(26, 30)); p.ClosePath(); p.Fill();
                // Goggles across the eyes.
                p.fillColor = Tone(Hex(0x9FD3C7)); p.strokeColor = Tone(Hex(0x215E59)); p.lineWidth = 2 * s;
                p.BeginPath(); p.MoveTo(P(32, 33)); p.LineTo(P(52, 33)); p.Arc(P(52, 37.5f), 4.5f * s, Angle.Degrees(-90), Angle.Degrees(90));
                p.LineTo(P(32, 42)); p.Arc(P(32, 37.5f), 4.5f * s, Angle.Degrees(90), Angle.Degrees(270)); p.ClosePath(); p.Fill(); p.Stroke();
            }
            p.fillColor = Tone(Ink);
            if (role != CrewRole.Technician) { Circle(p, P(36, 38), 2.4f * s); p.Fill(); Circle(p, P(48, 38), 2.4f * s); p.Fill(); }
            p.strokeColor = Tone(Ink); p.lineWidth = 2.4f * s; p.lineCap = LineCap.Round;
            p.BeginPath();
            if (role == CrewRole.Stocker) { p.MoveTo(P(36, 46)); p.LineTo(P(48, 46)); }
            else { p.MoveTo(P(36, 45)); p.QuadraticCurveTo(P(42, 50), P(48, 45)); }
            p.Stroke();
        }
    }

    /// <summary>A catalogue thumbnail on the arrange tab: a simple filled drawing per furniture type, decor piece, expansion or station.</summary>
    public sealed class FurnitureThumb : VisualElement
    {
        public const string ClassName = "es-plan-thumb";
        /// <summary>The hand siphon, for the research path's equipment row.</summary>
        public const string SiphonLook = "siphon";
        static readonly Color Ink = new Color(0.105f, 0.255f, 0.245f);
        static readonly Color Water = new Color(0.62f, 0.83f, 0.86f);
        static readonly Color Wood = new Color(0.79f, 0.6f, 0.4f);
        static readonly Color Metal = new Color(0.67f, 0.74f, 0.72f);
        static readonly Color Teal = new Color(0.13f, 0.37f, 0.35f);
        static readonly Color Coral = new Color(0.84f, 0.38f, 0.27f);
        static readonly Color Honey = new Color(0.98f, 0.78f, 0.37f);
        static readonly Color Paper = new Color(1f, 0.96f, 0.87f);
        string look = "";

        public string Look => look;

        public FurnitureThumb()
        {
            AddToClassList(ClassName);
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public void Set(string value)
        {
            value = value ?? "";
            if (value == look) return;
            look = value;
            MarkDirtyRepaint();
        }

        static void Box(Painter2D p, Vector2 a, Vector2 b, Color fill, float line)
        {
            p.fillColor = fill; p.strokeColor = Ink; p.lineWidth = line;
            p.BeginPath(); p.MoveTo(a); p.LineTo(new Vector2(b.x, a.y)); p.LineTo(b); p.LineTo(new Vector2(a.x, b.y)); p.ClosePath(); p.Fill(); p.Stroke();
        }

        static void Disc(Painter2D p, Vector2 at, float radius, Color fill)
        {
            p.fillColor = fill; p.BeginPath(); p.Arc(at, radius, Angle.Degrees(0), Angle.Degrees(360)); p.ClosePath(); p.Fill();
        }

        void Draw(MeshGenerationContext context)
        {
            Rect r = contentRect;
            if (r.width <= 0 || r.height <= 0) return;
            float s = Mathf.Min(r.width / 96f, r.height / 64f);
            Vector2 o = new Vector2(r.center.x - 48 * s, r.center.y - 32 * s);
            Vector2 P(float x, float y) => o + new Vector2(x * s, y * s);
            var p = context.painter2D;
            float line = 2.5f * s;
            p.lineJoin = LineJoin.Round; p.lineCap = LineCap.Round;
            switch (look)
            {
                case ShopLayout.SmallDisplayId:
                    Box(p, P(28, 10), P(68, 46), Water, line); Box(p, P(24, 46), P(72, 56), Wood, line); Disc(p, P(44, 30), 5 * s, Coral); break;
                case ShopLayout.LargeDisplayId:
                    Box(p, P(10, 10), P(86, 46), Water, line); Box(p, P(6, 46), P(90, 56), Wood, line);
                    Disc(p, P(34, 30), 5 * s, Coral); Disc(p, P(62, 26), 5 * s, Honey); break;
                case ShopLayout.TableDefinitionId:
                    Box(p, P(14, 22), P(82, 30), Wood, line); Box(p, P(20, 30), P(26, 54), Wood, line); Box(p, P(70, 30), P(76, 54), Wood, line); break;
                case ShopLayout.RackDefinitionId:
                    Box(p, P(26, 6), P(70, 58), Metal, line); Box(p, P(30, 12), P(66, 30), Water, line); Box(p, P(30, 36), P(66, 54), Water, line); break;
                case ShopLayout.ConditionerDefinitionId:
                    Box(p, P(22, 14), P(74, 50), new Color(0.75f, 0.66f, 0.85f), line); Box(p, P(18, 50), P(78, 58), Teal, line);
                    Disc(p, P(38, 32), 4 * s, Honey); Disc(p, P(56, 30), 4 * s, Honey); break;
                case ShopLayout.CoralPlanterId:
                    p.strokeColor = Coral; p.lineWidth = 5 * s;
                    p.BeginPath(); p.MoveTo(P(48, 40)); p.LineTo(P(48, 14)); p.MoveTo(P(48, 28)); p.LineTo(P(36, 16)); p.MoveTo(P(48, 24)); p.LineTo(P(60, 12)); p.Stroke();
                    Box(p, P(32, 40), P(64, 58), Wood, line); break;
                case ShopLayout.BubbleLampId:
                    Disc(p, P(48, 22), 14 * s, Honey); Box(p, P(45, 34), P(51, 52), Metal, line); Box(p, P(34, 52), P(62, 58), Teal, line);
                    Disc(p, P(68, 14), 3 * s, Water); Disc(p, P(74, 24), 2 * s, Water); break;
                case ShopLayout.SillyStatueId:
                    p.fillColor = Honey; p.BeginPath(); p.MoveTo(P(30, 26)); p.BezierCurveTo(P(36, 10), P(60, 10), P(64, 26)); p.LineTo(P(76, 16)); p.LineTo(P(76, 36));
                    p.LineTo(P(64, 26)); p.BezierCurveTo(P(60, 42), P(36, 42), P(30, 26)); p.ClosePath(); p.Fill();
                    Disc(p, P(40, 24), 2.5f * s, Ink); Box(p, P(34, 42), P(62, 58), Metal, line); break;
                case "expand-store":
                    Box(p, P(14, 22), P(54, 56), Paper, line); p.fillColor = Coral; p.BeginPath(); p.MoveTo(P(10, 24)); p.LineTo(P(34, 8)); p.LineTo(P(58, 24)); p.ClosePath(); p.Fill();
                    Plus(p, P(74, 36), 11 * s, line * 1.4f); break;
                case "expand-lab":
                    Box(p, P(14, 22), P(54, 56), new Color(0.84f, 0.92f, 0.9f), line); p.fillColor = Teal; p.BeginPath(); p.MoveTo(P(10, 24)); p.LineTo(P(34, 8)); p.LineTo(P(58, 24)); p.ClosePath(); p.Fill();
                    Plus(p, P(74, 36), 11 * s, line * 1.4f); break;
                case ShopLayout.BloomFacilityId: case ShopLayout.FlowFacilityId:
                    Box(p, P(32, 10), P(64, 46), look == ShopLayout.BloomFacilityId ? new Color(0.94f, 0.48f, 0.35f) : new Color(0.31f, 0.66f, 0.75f), line);
                    Box(p, P(26, 46), P(70, 56), Teal, line); break;
                case ShopLayout.IncubatorFacilityId:
                    Box(p, P(18, 18), P(78, 54), Metal, line); Disc(p, P(48, 34), 11 * s, Honey); Box(p, P(30, 8), P(66, 18), Teal, line); break;
                case ShopLayout.UpgradeFacilityId:
                    Box(p, P(12, 30), P(84, 38), Wood, line); Box(p, P(18, 38), P(24, 56), Wood, line); Box(p, P(72, 38), P(78, 56), Wood, line);
                    Box(p, P(30, 10), P(56, 30), Teal, line); Box(p, P(34, 14), P(52, 26), Water, line); break;
                case SiphonLook:
                    // A bulb on a glass tube with a nozzle, as the siphon held in the player's hand.
                    Box(p, P(38, 22), P(58, 50), new Color(0.84f, 0.92f, 0.9f), line);
                    Disc(p, P(48, 16), 10 * s, Coral);
                    Box(p, P(44, 50), P(52, 60), Teal, line); break;
                default:
                    Box(p, P(26, 14), P(70, 54), Paper, line); break;
            }
        }

        static void Plus(Painter2D p, Vector2 at, float half, float width)
        {
            p.strokeColor = Teal; p.lineWidth = width;
            p.BeginPath(); p.MoveTo(at + new Vector2(-half, 0)); p.LineTo(at + new Vector2(half, 0)); p.MoveTo(at + new Vector2(0, -half)); p.LineTo(at + new Vector2(0, half)); p.Stroke();
        }
    }

    /// <summary>An On / Off switch: a focusable button with a sliding knob. The state is also said by its label beside it.</summary>
    public sealed class PlanSwitch : Button
    {
        public const string ClassName = "es-plan-switch";
        public const string OnClass = "es-plan-switch--on";
        readonly VisualElement knob;

        public bool On { get; private set; }

        public PlanSwitch(System.Action onClick) : base(onClick)
        {
            AddToClassList("es-button");
            AddToClassList(ClassName);
            knob = new VisualElement { pickingMode = PickingMode.Ignore };
            knob.AddToClassList("es-plan-switch__knob");
            Add(knob);
        }

        public void SetOn(bool value)
        {
            if (value == On && ClassListContains(OnClass) == value) return;
            On = value;
            EnableInClassList(OnClass, value);
        }
    }
}
