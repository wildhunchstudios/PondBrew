using System.Collections.Generic;

namespace EvolutionShop
{
    /// <summary>AD-C: one character of the diorama kit. Every role shares the humanoid rig (the classic joint names and the AD-A
    /// face), so walking, reactions, comedy and work poses drive them all; roles differ by proportions, outfit, props, colours
    /// and how their face leans into each reaction.</summary>
    public sealed class DioramaRole
    {
        public readonly string Id, Kit;
        public readonly bool Crew;
        /// <summary>Kit shoulder height (m): the classic carry socket's place is scaled by it against the classic 1.17.</summary>
        public readonly float Shoulder;
        /// <summary>Top of the head and hat (m), as the kit script measures it; the slap stars circle above it.</summary>
        public readonly float HeadTop;
        /// <summary>How far the lids close while haggling or concentrating (0..1): a bargain hunter's squint.</summary>
        public readonly float Squint;
        /// <summary>How far the left brow tilts up while haggling or concentrating (degrees): a collector's raised brow.</summary>
        public readonly float BrowTilt;

        public DioramaRole(string id, string kit, bool crew, float shoulder, float headTop, float squint, float browTilt)
        {
            Id = id; Kit = kit; Crew = crew; Shoulder = shoulder; HeadTop = headTop; Squint = squint; BrowTilt = browTilt;
        }

        public const float ClassicShoulder = 1.17f;
        public float CarryScale => Shoulder / ClassicShoulder;
        public float StarHeight => HeadTop + .13f;
    }

    public static class DioramaRoles
    {
        /// <summary>The four customer types (AD.4: Casual round, Bargain tall and thin, Collector pear, Big spender a wide coat) and
        /// the three crew (Pip at the counter, on a step stool; Nori the stocker; Moss the technician), always in shop teal.</summary>
        public static readonly IReadOnlyList<DioramaRole> All = new[]
        {
            new DioramaRole("casual", "DioramaCasualVisitor", false, .86f, 1.69f, 0f, 10f),
            new DioramaRole("bargain", "DioramaBargainVisitor", false, 1.04f, 1.845f, .35f, 14f),
            new DioramaRole("collector", "DioramaCollectorVisitor", false, .83f, 1.72f, .15f, 22f),
            new DioramaRole("premium", "DioramaPremiumVisitor", false, .88f, 1.93f, .45f, 4f),
            new DioramaRole("cashier", "DioramaCrewCashier", true, 1.16f, 1.96f, .2f, 6f),
            new DioramaRole("stocker", "DioramaCrewStocker", true, .86f, 1.76f, .2f, 6f),
            new DioramaRole("technician", "DioramaCrewTechnician", true, .95f, 1.82f, .25f, 12f),
        };

        public static readonly IReadOnlyList<string> Customers = new[] { "casual", "bargain", "collector", "premium" };
        public static readonly IReadOnlyList<string> CrewRoles = new[] { "cashier", "stocker", "technician" };

        public static bool TryGet(string id, out DioramaRole role)
        {
            foreach (var r in All) if (r.Id == id) { role = r; return true; }
            role = null;
            return false;
        }

        /// <summary>The crew role of an employee id (ShopStaff), or null.</summary>
        public static string CrewRoleOf(string employeeId) =>
            employeeId == ShopStaff.CashierId ? "cashier" : employeeId == ShopStaff.StockerId ? "stocker" :
            employeeId == ShopStaff.TechnicianId ? "technician" : null;

        /// <summary>The four reactions each role shows: customers haggle, are pleased, shocked or slapped; crew concentrate,
        /// are pleased, are caught out, and greet.</summary>
        public static IReadOnlyList<DioramaVisitorMood> Reactions(DioramaRole role) => role != null && role.Crew
            ? new[] { DioramaVisitorMood.Focus, DioramaVisitorMood.Happy, DioramaVisitorMood.Shocked, DioramaVisitorMood.Greet }
            : new[] { DioramaVisitorMood.Haggle, DioramaVisitorMood.Happy, DioramaVisitorMood.Shocked, DioramaVisitorMood.Ouch };
    }
}
