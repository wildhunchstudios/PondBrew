using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class ShopSession
    {
        GUIStyle titleStyle, headingStyle, bodyStyle, smallStyle, buttonStyle, statStyle, centerStyle, controlsStyle;
        readonly Color ink = new Color(0.035f, 0.08f, 0.105f, 0.96f);
        readonly Color mint = new Color(0.5f, 0.95f, 0.77f);
        Vector2 panelScroll;
        void Styles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label) {fontSize = 28, fontStyle = FontStyle.Bold};
            titleStyle.normal.textColor = Color.white;
            headingStyle = new GUIStyle(titleStyle) {fontSize = 20};
            bodyStyle = new GUIStyle(GUI.skin.label) {fontSize = 16, wordWrap = true};
            bodyStyle.normal.textColor = new Color(0.86f, 0.93f, 0.94f);
            smallStyle = new GUIStyle(bodyStyle) {fontSize = 13};
            smallStyle.normal.textColor = new Color(0.63f, 0.78f, 0.80f);
            buttonStyle = new GUIStyle(GUI.skin.button) {fontSize = 16, fontStyle = FontStyle.Bold, wordWrap = true, padding = new RectOffset(14,14,10,10)};
            statStyle = new GUIStyle(headingStyle); statStyle.normal.textColor = mint;
            centerStyle = new GUIStyle(bodyStyle) {alignment = TextAnchor.MiddleCenter};
            controlsStyle = new GUIStyle(smallStyle) {alignment = TextAnchor.MiddleCenter, wordWrap = false};
        }
        static void Box(Rect rect, Color color)
        {
            Color before = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = before;
        }
        void OnGUI()
        {
            if (!initialized) return;
            Styles();
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1280 * scale) / 2, (Screen.height - 720 * scale) / 2, 0), Quaternion.identity, Vector3.one * scale);
            Box(new Rect(22, 22, 338, 78), ink);
            Box(new Rect(22, 22, 4, 78), mint);
            GUI.Label(new Rect(40, 29, 310, 32), "EVOLUTION SHOP", titleStyle);
            GUI.Label(new Rect(40, 65, 310, 25), "THE LITTLE LAB OF NEW LIFE", smallStyle);
            Box(new Rect(1050, 22, 208, 78), ink);
            GUI.Label(new Rect(1069, 31, 175, 30), Model.Currency + "  CREDITS", statStyle);
            GUI.Label(new Rect(1069, 63, 175, 23), Model.DiscoveredSpeciesIds.Count + " / 3 discovered", smallStyle);
            Box(new Rect(22, 116, 310, 82), ink);
            GUI.Label(new Rect(38, 125, 275, 21), "YOUR NEXT STEP", smallStyle);
            GUI.Label(new Rect(38, 149, 278, 44), Objective(), bodyStyle);
            Box(new Rect(22, 610, 396, 88), ink);
            GUI.Label(new Rect(40, 620, 360, 28), "BLOOM " + Model.Available(CultureKind.Bloom) + "     FLOW " + Model.Available(CultureKind.Flow), headingStyle);
            GUI.Label(new Rect(40, 652, 360, 30), "Siphon " + Model.ReservedCultureCount + " / " + (BaselinePreview ? Config.BaseCapacity : Model.Capacity) + "  |  Staged " + (Model.Staged(CultureKind.Bloom) + Model.Staged(CultureKind.Flow)), smallStyle);
            Box(new Rect(438,662,548,36),ink);
            GUI.Label(new Rect(448,668,528,23), "WASD move   |   E interact   |   TAB journal   |   ESC pause", controlsStyle);
            string machine = Model.Incubator.Phase == IncubatorPhase.Incubating ? "INCUBATING  " + Mathf.CeilToInt(Model.Incubator.SecondsRemaining) + "s" : Model.Incubator.Phase == IncubatorPhase.Ready ? "SPECIMEN READY" : "INCUBATOR AVAILABLE";
            Box(new Rect(1050, 116, 208, 68), ink);
            GUI.Label(new Rect(1067, 126, 175, 48), machine, bodyStyle);
            if (Panel == ShopPanel.None)
            {
                Box(new Rect(637, 357, 6, 6), new Color(1,1,1,0.85f));
                if (Focused != null)
                {
                    Box(new Rect(420, 525, 440, 67), ink);
                    GUI.Label(new Rect(435, 531, 410, 26), Focused.Label, centerStyle);
                    bool culture = Focused.Kind == StationKind.Bloom || Focused.Kind == StationKind.Flow;
                    string hint = culture ? "Hold LEFT MOUSE to collect" : "Press E to interact";
                    GUI.Label(new Rect(435, 556, 410, 26), hint, centerStyle);
                    if (HarvestProgress > 0) {Box(new Rect(460, 598, 360, 5), ink); Box(new Rect(460, 598, 360 * HarvestProgress, 5), mint);}
                }
            }
            if (Panel != ShopPanel.None) DrawPanel();
            if (!string.IsNullOrEmpty(message) && Time.unscaledTime < messageUntil)
            {
                Box(new Rect(380, 24, 650, 67), ink);
                GUI.Label(new Rect(400, 32, 610, 54), message, centerStyle);
            }
            if (Panel == ShopPanel.None && Time.unscaledTime < revealUntil && !string.IsNullOrEmpty(revealedSpecies))
            {
                var species = Model.GetSpecies(revealedSpecies);
                Box(new Rect(22, 212, 310, 137), ink);
                GUI.Label(new Rect(38, 222, 278, 25), "DISCOVERED / JOURNAL UPDATED", centerStyle);
                GUI.Label(new Rect(40, 252, 278, 42), species.DisplayName, titleStyle);
                GUI.Label(new Rect(38, 298, 278, 30), species.Bloom + " Bloom + " + species.Flow + " Flow", centerStyle);
            }
            if (BaselinePreview) GUI.Label(new Rect(440, 102, 520, 27), "DEVELOPMENT COMPARISON: BASE SIPHON (F8 TO RESTORE)", smallStyle);
            GUI.matrix = Matrix4x4.identity;
        }
        string Objective()
        {
            if (Model.Incubator.Phase == IncubatorPhase.Ready) return "Head to a display tank in the front store.";
            if (Model.Incubator.Phase == IncubatorPhase.Incubating) return "New life is growing. Gather your next cultures.";
            if (Model.GetTank("tank-a") != null || Model.GetTank("tank-b") != null) return "Visit the sales counter to sell a stocked creature.";
            if (Model.DiscoveredSpeciesIds.Count == 0) return "Back lab: mix one Bloom + one Flow.";
            if (!Model.UpgradeOwned && Model.Currency >= Config.UpgradeCost) return "Buy the larger siphon at the back lab bench.";
            if (!Model.UpgradeOwned) return "Grow and sell creatures. Save " + Config.UpgradeCost + " credits for a larger siphon.";
            if (Model.DiscoveredSpeciesIds.Count < 3) return "Discover the remaining creatures by changing one dose.";
            return "Milestone loop complete. Try making your favorite again.";
        }
        void DrawPanel()
        {
            Box(new Rect(0,0,1280,720), new Color(0.015f,0.045f,0.06f,0.38f));
            Box(new Rect(350,112,580,518), ink);
            Box(new Rect(350,112,580,3), mint);
            GUILayout.BeginArea(new Rect(377,130,526,477));
            panelScroll = GUILayout.BeginScrollView(panelScroll, GUIStyle.none, GUI.skin.verticalScrollbar);
            switch (Panel)
            {
                case ShopPanel.Welcome:
                    Heading("Welcome to your little shop");
                    Text("Front store: display tanks and sales counter. Through the doorway: your back lab, with growers, incubator, and upgrade bench.");
                    Space();
                    Text("WASD to move / mouse to look\nHold LEFT MOUSE at a grower to collect\nE to use a station / TAB for your journal\nESC to pause, change settings, or save");
                    Space();
                    Text("Start in the back lab: mix one Bloom and one Flow in the incubator. Bring the result to a front display tank, then sell it at the counter. Growers are free.");
                    Small("One local save. The lab pauses while closed. This is a first playable with provisional recipes and balance.");
                    Space(); Button("Open the shop", () => SetPanel(ShopPanel.None));
                    break;
                case ShopPanel.Incubator: DrawIncubator(); break;
                case ShopPanel.Counter: DrawCounter(); break;
                case ShopPanel.Upgrade: DrawUpgrade(); break;
                case ShopPanel.Journal: DrawJournal(); break;
                case ShopPanel.Tank:
                    Heading(selectedTank == "tank-a" ? "Display tank A" : "Display tank B");
                    var resident = Model.GetTank(selectedTank);
                    Text(resident == null ? "This tank is empty. When a specimen is ready, press E here to transfer it." : Model.GetSpecies(resident.Value.SpeciesId).DisplayName + " lives here. Visit the sales counter to sell this specimen.");
                    Small("Capacity: one specimen per tank. A full tank never consumes a waiting specimen.");
                    break;
                case ShopPanel.Pause:
                    Heading("Shop paused"); Text("Growers and incubation are paused. Your progress saves automatically.");
                    Space(); Text("Mouse sensitivity: " + Sensitivity.ToString("0.00"));
                    Sensitivity = GUILayout.HorizontalSlider(Sensitivity, 0.25f, 2.5f);
                    Space(); Text("Sound volume: " + Mathf.RoundToInt(Volume * 100) + "%");
                    Volume = GUILayout.HorizontalSlider(Volume, 0, 1); AudioListener.volume = Volume;
                    Space(); Button("Save and resume", () => { SaveNow(); SetPanel(ShopPanel.None); });
                    Button("Reload local save", () => { if (ReloadSaved()) { SetPanel(ShopPanel.None); Notify("Local save restored."); } });
                    Button("New game...", () => SetPanel(ShopPanel.Reset));
                    Button("Save and quit", Quit);
                    Small("Simulation does not advance while the application is closed.");
                    break;
                case ShopPanel.Reset:
                    Heading("Start a new game?");
                    Text("This replaces your one local save: currency, cultures, discoveries, creatures, upgrade, and incubation progress will reset.");
                    Space(); Button("Cancel / keep my shop", () => SetPanel(ShopPanel.Pause));
                    Space(); Button("Confirm new game", ConfirmNewGame);
                    break;
            }
            if (Panel != ShopPanel.Welcome && Panel != ShopPanel.Pause && Panel != ShopPanel.Reset)
            { Space(); Button("Close  /  ESC", () => SetPanel(ShopPanel.None)); }
            GUILayout.EndScrollView(); GUILayout.EndArea();
        }
        void DrawIncubator()
        {
            Heading("The incubator");
            var inc = Model.Incubator;
            if (inc.Phase == IncubatorPhase.Incubating)
            {
                Text("Your " + Model.GetSpecies(inc.SpeciesId).DisplayName + " is growing.");
                Heading(Mathf.CeilToInt(inc.SecondsRemaining) + " seconds remaining");
                Text("Feel free to gather cultures for the next specimen. This cycle continues while you work.");
                return;
            }
            if (inc.Phase == IncubatorPhase.Ready)
            { Text(Model.GetSpecies(inc.SpeciesId).DisplayName + " is ready!"); Text("Return through the doorway to an empty tank in the front store, then press E to transfer it."); return; }
            Text("Add exact integer doses. One recipe makes one specimen.");
            Heading(inc.Bloom + " BLOOM  +  " + inc.Flow + " FLOW");
            GUILayout.BeginHorizontal();
            Button("+ Bloom", () => Perform(Model.Dose(CultureKind.Bloom)));
            Button("+ Flow", () => Perform(Model.Dose(CultureKind.Flow)));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            Button("- Bloom", () => Perform(Model.ReturnDose(CultureKind.Bloom)));
            Button("- Flow", () => Perform(Model.ReturnDose(CultureKind.Flow)));
            GUILayout.EndHorizontal();
            Button("Begin incubation  /  " + Config.IncubationSeconds + " sec", () => { if (Perform(Model.BeginIncubation()).Success) SetPanel(ShopPanel.None); });
            Button("Return all doses safely", () => Perform(Model.ClearDoses()));
            Small("Doses reserve room in your siphon until incubation begins. Unsupported mixtures never consume resources.");
            Space(); Small("LAB NOTES: A balanced pair brings a Pondlet. More Bloom makes a rounder body; more Flow makes a longer tail.");
            foreach (var species in Config.Species)
                if (Model.IsDiscovered(species.Id)) Button("Prepare " + species.DisplayName + "  (" + species.Bloom + "B + " + species.Flow + "F)", () => Perform(Model.UseKnownRecipe(species.Id)));
        }
        void DrawCounter()
        {
            Heading("The sales counter"); Text("Choose a stocked specimen to sell. Its discovery stays in your journal.");
            foreach (var id in Config.TankIds)
            {
                var fish = Model.GetTank(id);
                if (fish == null) { Small(id == "tank-a" ? "Tank A / empty" : "Tank B / empty"); continue; }
                var species = Model.GetSpecies(fish.Value.SpeciesId);
                string tank = id;
                Space(); Button("Sell " + species.DisplayName + " from " + (id == "tank-a" ? "A" : "B") + "  /  +" + species.SaleValue + " credits", () => Perform(Model.SellFromTank(tank)));
            }
            Space(); Small("Only creatures in display tanks can be sold. Each sale removes that exact specimen and pays once.");
        }
        void DrawUpgrade()
        {
            Heading("A little more room to grow");
            Text("SIPHON MK II\nA visibly larger chamber and faster collection.");
            Space(); Heading(Config.BaseCapacity + "  to  " + Config.UpgradedCapacity + " culture units");
            Text("Collection time: " + Config.BaseCollectionSeconds.ToString("0.0") + "s to " + Config.UpgradedCollectionSeconds.ToString("0.0") + "s per unit.");
            Space();
            if (Model.UpgradeOwned) Text("Installed. Your upgraded siphon is ready.");
            else Button("Buy siphon MK II  /  " + Config.UpgradeCost + " credits", () => Perform(Model.BuyUpgrade()));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Model.UpgradeOwned) {Space(); Button(BaselinePreview ? "DEV: restore upgraded siphon" : "DEV: compare original siphon", () => SetBaselinePreview(!BaselinePreview)); Small("F8 toggles comparison. Empty down to four cultures first. Ownership and save data remain unchanged.");}
#endif
        }
        void DrawJournal()
        {
            Heading("Field journal"); Text("Every new discovery is recorded here, even after its specimen is sold.");
            foreach (var species in Config.Species)
            {
                Space();
                if (Model.IsDiscovered(species.Id))
                { Heading(species.DisplayName); Text(species.Bloom + " Bloom + " + species.Flow + " Flow  /  " + species.SaleValue + " credits"); }
                else {Heading("Unknown specimen"); Small(species.Id == "pondlet" ? "Begin with a balanced pair." : species.Id == "bubblebelly" ? "A little more Bloom may round things out." : "A little more Flow may stretch a tail.");}
            }
            Space(); Small("Known recipes can be prepared in one click at the incubator, using cultures you have collected.");
        }
        void Heading(string text) => GUILayout.Label(text, headingStyle);
        void Text(string text) => GUILayout.Label(text, bodyStyle);
        void Small(string text) => GUILayout.Label(text, smallStyle);
        void Space() => GUILayout.Space(12);
        void Button(string text, System.Action action) { if (GUILayout.Button(text, buttonStyle, GUILayout.MinHeight(38))) action(); }
    }
}
