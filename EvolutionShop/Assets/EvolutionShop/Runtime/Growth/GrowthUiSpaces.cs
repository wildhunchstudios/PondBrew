using EvolutionShop.Growth;

namespace EvolutionShop
{
    /// <summary>
    /// GR-10's cards (GR.11) in GR-13's style: shop unit C, opened at its partition in unit B's north-east arch, and the
    /// exhibition hall finish, bought at its plans on unit C's north wall, each a purchase with its gates as chips. Both
    /// open at night; the room then opens with its partition coming down (the hall with its banners and spotlights), and a
    /// banner names it.
    /// </summary>
    public sealed partial class GrowthUi
    {
        void FillUnitC()
        {
            var shop = s.Shop;
            var unit = GrowthShop.UnitCDef;
            var body = Head("Shop unit C", shop.UnitC ? "open" : unit.Credits + " credits", null, "Through an arch in unit B's north wall. Walls open at night.");
            if (shop.UnitC) { body.Add(L("Open: six more places, two of them on a feature wall for Show tanks, and one more visitor at a time.", "gr-hint")); Footer(body); return; }
            var failure = shop.UnitCCheck(out string why);
            BuyPanel(body, "The room behind unit B", "Six more places", "An arch into the room behind unit B: six more places, two of them on a feature wall for Show tanks, and one more visitor at a time.",
                Gates(unit.Era, unit.Stars, unit.Credits, 0, true), failure, why, "Open unit C for " + unit.Credits + " cr", shop.BuyUnitC);
            Footer(body);
        }

        void FillHall()
        {
            var shop = s.Shop;
            // GR-11: once the hall is finished, its board is the Grand Exhibition's.
            if (shop.Hall) { FillExhibition(); return; }
            var hall = GrowthShop.HallDef;
            var body = Head("Exhibition hall plans", hall.Credits + " credits", null, "Then this board is where the Grand Exhibition is prepared. The hall is finished at night.");
            var failure = shop.HallCheck(out string why);
            BuyPanel(body, "Finish the shop as an exhibition hall", "Banners, spotlights and two exhibition places", "Banners in every unit, spotlights on the feature places, two exhibition places along unit B's front wall either side of the bay window, and one more visitor at a time.",
                Gates(hall.Era, hall.Stars, hall.Credits, 0, true), failure, why, "Finish the hall for " + hall.Credits + " cr", shop.BuyHall);
            Footer(body);
        }

        string SpacesSignature() => s.Shop.UnitC + "|" + s.Shop.Hall;
    }
}
