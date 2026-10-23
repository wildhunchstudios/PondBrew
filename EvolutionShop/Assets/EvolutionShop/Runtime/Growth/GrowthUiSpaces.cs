using EvolutionShop.Growth;

namespace EvolutionShop
{
    /// <summary>
    /// GR-10's cards (GR.11): shop unit C, opened at its partition in unit B's north-east arch, and the exhibition hall
    /// finish, bought at its plans on unit C's north wall. Both open at night; the room then opens with its partition coming
    /// down (the hall with its banners and spotlights), and a banner names it.
    /// </summary>
    public sealed partial class GrowthUi
    {
        void FillUnitC()
        {
            var shop = s.Shop;
            var unit = GrowthShop.UnitCDef;
            var body = Head("Shop unit C", shop.UnitC ? "open" : unit.Credits + " credits");
            body.Add(Text("Open an arch in unit B's north wall into the room behind it: six more places, two of them on a feature wall for Show tanks, and one more visitor at a time."));
            body.Add(Text("Needs unit B, era 4, 4 stars and " + unit.Credits + " credits, at night.", "es-text--muted"));
            var failure = shop.UnitCCheck(out string why);
            if (!shop.UnitC) body.Add(Button(failure == GrowthFailure.None ? "Open unit C for " + unit.Credits + " cr" : why, () => { if (s.Act(shop.BuyUnitC).Success) s.SetCard(GrowthCard.None); }, true, failure == GrowthFailure.None));
            Footer(body);
        }

        void FillHall()
        {
            var shop = s.Shop;
            // GR-11: once the hall is finished, its board is the Grand Exhibition's.
            if (shop.Hall) { FillExhibition(); return; }
            var hall = GrowthShop.HallDef;
            var body = Head(shop.Hall ? "Exhibition hall" : "Exhibition hall plans", shop.Hall ? "finished" : hall.Credits + " credits");
            body.Add(Text("Finish the shop as an exhibition hall: banners in every unit, spotlights on the feature places, two exhibition places along unit B's front wall either side of the bay window, and one more visitor at a time. Then this board is where the Grand Exhibition is prepared."));
            if (!shop.Hall)
            {
                body.Add(Text("Needs unit C, era 5, 5 stars and " + hall.Credits + " credits, at night.", "es-text--muted"));
                var failure = shop.HallCheck(out string why);
                body.Add(Button(failure == GrowthFailure.None ? "Finish the hall for " + hall.Credits + " cr" : why, () => { if (s.Act(shop.BuyHall).Success) s.SetCard(GrowthCard.None); }, true, failure == GrowthFailure.None));
            }
            Footer(body);
        }

        string SpacesSignature() => s.Shop.UnitC + "|" + s.Shop.Hall;
    }
}
