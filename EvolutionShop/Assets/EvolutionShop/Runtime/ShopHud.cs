using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class ShopSession
    {
        GUIStyle titleStyle, headingStyle, bodyStyle, smallStyle, buttonStyle, statStyle, centerStyle, controlsStyle;
        readonly Color ink = new Color(0.035f, 0.08f, 0.105f, 0.96f);
        readonly Color mint = new Color(0.5f, 0.95f, 0.77f);
        Vector2 panelScroll;
        ShopTradeLedger reportDisplayLedger;
        TradeDaySave reportDisplaySnapshot;
        int reportDisplayDay = -1;
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
            GUI.Label(new Rect(40, 65, 310, 25), ReputationLine(), smallStyle);
            Box(new Rect(1050, 22, 208, 78), ink);
            GUI.Label(new Rect(1069, 31, 175, 30), Model.Currency + "  CREDITS", statStyle);
            GUI.Label(new Rect(1069, 63, 175, 23), Model.DiscoveredSpeciesIds.Count + " / " + Config.Species.Length + " discovered", smallStyle);
            Box(new Rect(22, 116, 310, 82), ink);
            GUI.Label(new Rect(38, 125, 275, 21), "YOUR NEXT STEP", smallStyle);
            GUI.Label(new Rect(38, 149, 278, 44), Objective(), bodyStyle);
            Box(new Rect(22, 212, 310, 84), ink);
            GUI.Label(new Rect(38, 222, 280, 28), PhaseTitle(), headingStyle);
            GUI.Label(new Rect(38, 255, 280, 33), Paused ? "PAUSED / " + ClockText() : ClockText(), smallStyle);
            Box(new Rect(22, 610, 396, 88), ink);
            string starterCultures = "BLOOM " + Model.Available(CultureKind.Bloom) + "  FLOW " + Model.Available(CultureKind.Flow);
            if (Model.MistResearchUnlocked)
            {
                GUI.Label(new Rect(40, 616, 360, 26), starterCultures, headingStyle);
                GUI.Label(new Rect(40, 641, 360, 26), "SPARK " + Model.Available(CultureKind.Spark) + "  MIST " + Model.Available(CultureKind.Mist), headingStyle);
            }
            else GUI.Label(new Rect(40, 620, 360, 28), starterCultures + (Model.SparkResearchUnlocked ? "  SPARK " + Model.Available(CultureKind.Spark) : ""), headingStyle);
            GUI.Label(new Rect(40, Model.MistResearchUnlocked ? 670 : 652, 360, 26), "Siphon " + Model.ReservedCultureCount + " / " + (BaselinePreview ? Config.BaseCapacity : Model.Capacity) + "  |  Staged " + (Model.Staged(CultureKind.Bloom) + Model.Staged(CultureKind.Flow) + Model.Staged(CultureKind.Spark) + Model.Staged(CultureKind.Mist)), smallStyle);
            Box(new Rect(438,662,548,36),ink);
            GUI.Label(new Rect(448,668,528,23), PlacementActive ? "WASD move   |   R rotate   |   ESC cancel placement" : "WASD move   |   E interact   |   TAB journal   |   ESC pause", controlsStyle);
            string machine = Model.Incubator.Phase == IncubatorPhase.Incubating ? "INCUBATING  " + Mathf.CeilToInt(Model.Incubator.SecondsRemaining) + "s" : Model.Incubator.Phase == IncubatorPhase.Ready ? "SPECIMEN READY" : "INCUBATOR AVAILABLE";
            Box(new Rect(1050, 116, 208, 68), ink);
            GUI.Label(new Rect(1067, 126, 175, 48), machine, bodyStyle);
            if (Cycle.Phase != ShopDayPhase.Night || Customers.ActiveCount > 0)
            {
                Box(new Rect(1050, 200, 208, 130), ink);
                GUI.Label(new Rect(1067, 209, 175, 23), "FRONT STORE", smallStyle);
                GUI.Label(new Rect(1067, 235, 175, 86), Customers.Status, bodyStyle);
            }
            if (Panel == ShopPanel.None && !PlacementActive)
            {
                Box(new Rect(637, 357, 6, 6), new Color(1,1,1,0.85f));
                if (SlapTarget != null)
                {
                    Box(new Rect(420, 446, 440, 67), ink);
                    GUI.Label(new Rect(435, 450, 410, 26), SlapTarget.ArchetypeName, centerStyle);
                    string slapHint = SlapCooldownRemaining > 0 ? "Slap ready in " + SlapCooldownRemaining.ToString("0.0") + "s" :
                        "Press " + SlapKey + " to slap / -1 reputation";
                    GUI.Label(new Rect(435, 477, 410, 26), slapHint, centerStyle);
                }
                if (Focused != null)
                {
                    Box(new Rect(420, 525, 440, 67), ink);
                    GUI.Label(new Rect(435, 531, 410, 26), Focused.Label, centerStyle);
                    bool culture = TryStationCulture(Focused, out var focusCulture);
                    string hint = culture ? (focusCulture == CultureKind.Spark ? "Hold LEFT MOUSE: Spark / E: research" : focusCulture == CultureKind.Mist ? "Hold LEFT MOUSE: Mist / E: vat" : "Hold LEFT MOUSE to collect") : "Press E to interact";
                    GUI.Label(new Rect(435, 556, 410, 26), hint, centerStyle);
                    if (HarvestProgress > 0) {Box(new Rect(460, 598, 360, 5), ink); Box(new Rect(460, 598, 360 * HarvestProgress, 5), mint);}
                }
            }
            if (Panel != ShopPanel.None) DrawPanel();
            if (PlacementActive) DrawPlacementOverlay();
            if (!string.IsNullOrEmpty(SaveError) || (!string.IsNullOrEmpty(message) && Time.unscaledTime < messageUntil))
            {
                Box(new Rect(380, 24, 650, 67), ink);
                GUI.Label(new Rect(400, 32, 610, 54), string.IsNullOrEmpty(SaveError) ? message : "PROGRESS NOT SAVED - " + SaveError, centerStyle);
            }
            if (Panel == ShopPanel.None && Time.unscaledTime < revealUntil && !string.IsNullOrEmpty(revealedSpecies))
            {
                var species = Model.GetSpecies(revealedSpecies);
                Box(new Rect(22, 310, 310, 137), ink);
                GUI.Label(new Rect(38, 320, 278, 25), "DISCOVERED / JOURNAL UPDATED", centerStyle);
                GUI.Label(new Rect(40, 350, 278, 42), species.DisplayName, titleStyle);
                GUI.Label(new Rect(38, 396, 278, 30), RecipeText(species), centerStyle);
            }
            if (BaselinePreview) GUI.Label(new Rect(440, 102, 520, 27), "DEVELOPMENT COMPARISON: BASE SIPHON (F8 TO RESTORE)", smallStyle);
            GUI.matrix = Matrix4x4.identity;
        }
        string Objective()
        {
            if (Customers.Visit != null && Customers.Visit.Phase == CustomerPhase.Queued) return "A customer is waiting. Use the front counter to serve.";
            if (Customers.Visit != null && Customers.Visit.Phase == CustomerPhase.ToCounter) return "A creature is reserved. Head to the front counter.";
            if (Cycle.Phase == ShopDayPhase.Closing) return "Closing up. Unsold stock stays safe for tomorrow.";
            if (Model.Incubator.Phase == IncubatorPhase.Ready) return Model.StorageIds.Length > 0 ? "Use an empty display or lab rack to store the ready creature." : "Head to a display tank in the front store.";
            if (ReadyConditionedCount > 0) return "Use E at an empty rack or display to collect vat output.";
            if (Model.Incubator.Phase == IncubatorPhase.Incubating) return "New life is growing. Gather your next cultures.";
            if (DisplayedStockCount() == 0 && StoredSpecimenCount > 0) return "Use E at an empty display to choose stored stock.";
            if (DisplayedStockCount() > 0) return Cycle.Phase == ShopDayPhase.Night ? "Stock is ready. Open for the day at the front counter." : "Let customers browse, then serve their orders at the counter.";
            if (Model.DiscoveredSpeciesIds.Count == 0) return "Back lab: mix one Bloom + one Flow.";
            if (Model.SparkResearchReady && !Model.SparkResearchUnlocked) return "At night: research Spark at the lab bench / " + Config.SparkResearchCost + " credits.";
            if (Model.SparkResearchUnlocked && !Model.IsDiscovered("lanternfin")) return "Try one Bloom + one Flow + one Spark.";
            if (Model.MistResearchReady && !Model.MistResearchUnlocked) return "At night: research Mist at the placed vat / " + Config.MistResearchCost + " credits.";
            if (Model.MistResearchUnlocked && !Model.IsDiscovered("driftwing")) return "Try one Bloom + one Flow + one Mist for an Airfin.";
            if (!Model.UpgradeOwned && Model.Currency >= Config.UpgradeCost) return "Buy the larger siphon at the back lab bench.";
            if (!Model.UpgradeOwned) return "Grow and sell creatures. Save " + Config.UpgradeCost + " credits for a larger siphon.";
            if (Model.DiscoveredSpeciesIds.Count < 3) return "Discover the remaining creatures by changing one dose.";
            return Cycle.Phase == ShopDayPhase.Night ? "Prepare your favorite creatures, then open at the counter." : "Keep stock flowing, or close for the night at the counter.";
        }
        string PhaseTitle() => Cycle.Phase == ShopDayPhase.Night ? "NIGHT / DAY " + Cycle.DayNumber : Cycle.Phase == ShopDayPhase.Open ? "DAY " + Cycle.DayNumber + " / OPEN" : "DAY " + Cycle.DayNumber + " / CLOSING";
        string ClockText()
        {
            if (Cycle.Phase == ShopDayPhase.Night) return "Untimed preparation / open at counter";
            int seconds = Mathf.CeilToInt(Cycle.SecondsRemaining);
            return (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00") + (Cycle.Phase == ShopDayPhase.Open ? " left to trade" : " until preparation night");
        }
        void DrawPanel()
        {
            bool dayReportPanel = Panel == ShopPanel.DayReport;
            bool closePanel = !dayReportPanel && Panel != ShopPanel.Welcome && Panel != ShopPanel.Pause && Panel != ShopPanel.Reset;
            bool fixedFooter = dayReportPanel || closePanel;
            Box(new Rect(0,0,1280,720), new Color(0.015f,0.045f,0.06f,0.38f));
            Box(new Rect(350,112,580,518), ink);
            Box(new Rect(350,112,580,3), mint);
            GUILayout.BeginArea(new Rect(377,130,526,fixedFooter ? 423 : 477));
            panelScroll = GUILayout.BeginScrollView(panelScroll, GUIStyle.none, GUI.skin.verticalScrollbar);
            switch (Panel)
            {
                case ShopPanel.Welcome:
                    Heading("Welcome to your little shop");
                    Text("Front store: display tanks and sales counter. Through the doorway: your back lab, with growers, incubator, and upgrade bench.");
                    Space();
                    Text("WASD to move / mouse to look\nHold LEFT MOUSE at a grower to collect\nE to use a station / TAB for your journal\nESC to pause, change settings, or save");
                    Space();
                    Text("Prepare at night: mix one Bloom and one Flow in the lab, then take your creature to a front tank. Open at the counter. Customers browse, choose a creature and wait for checkout. Use E at the counter and serve their order before their patience runs out.");
                    Text("Press E at a stocked tank to change its price tag. Four kinds of customer react to your price differently; if it is too high for them they walk away. The journal (TAB) describes each.");
                    Small("Nights are untimed. Growers are free. One local save; production and the clock stop while paused or the application is closed.");
                    Space(); Button("Enter your shop", () => SetPanel(ShopPanel.None));
                    break;
                case ShopPanel.Incubator: DrawIncubator(); break;
                case ShopPanel.Counter: DrawCounter(); break;
                case ShopPanel.DayReport: DrawDayReport(); break;
                case ShopPanel.Upgrade: DrawUpgrade(); break;
                case ShopPanel.Journal: DrawJournal(); break;
                case ShopPanel.Tank: DrawTank(); break;
                case ShopPanel.Arrange: DrawArrangement(); break;
                case ShopPanel.StorageRack: DrawSpecimenRack(); break;
                case ShopPanel.Conditioning: DrawConditioning(); break;
                case ShopPanel.Pause:
                    Heading("Shop paused"); Text("The day clock and all lab production are paused. Your progress saves automatically.");
                    Space(); Text("Mouse sensitivity: " + Sensitivity.ToString("0.00"));
                    Sensitivity = GUILayout.HorizontalSlider(Sensitivity, 0.25f, 2.5f);
                    Space(); Text("Sound volume: " + Mathf.RoundToInt(Volume * 100) + "%");
                    Volume = GUILayout.HorizontalSlider(Volume, 0, 1); AudioListener.volume = Volume;
                    Space(); Heading("Cartoon slap");
                    Small("Close panels, aim at a nearby unpaid customer and tap " + SlapKey + ". They leave; -1 reputation. Three-second cooldown.");
                    Button(WaitingForSlapKey ? "Press a letter / Escape cancels" : "Change slap key / " + SlapKey, BeginSlapRebind);
                    Text("Humor volume: " + Mathf.RoundToInt(HumorVolume * 100) + "%");
                    HumorVolume = GUILayout.HorizontalSlider(HumorVolume, 0, 1);
                    ReducedGags = GUILayout.Toggle(ReducedGags, "Reduced gag effects (no stars, smaller motion)");
                    Space(); Button("Save and resume", () => { if (SaveNow()) SetPanel(ShopPanel.None); });
                    Button("Reload local save", () => { if (ReloadSaved()) { SetPanel(ShopPanel.None); Notify("Local save restored. Unpaid visits cancelled; their creatures remain in the tanks."); } });
                    Button("New game...", () => SetPanel(ShopPanel.Reset));
                    Button("Save and quit", Quit);
                    Small("No offline progress. Reload cancels unpaid visits and releases their creatures. Completed sales stay saved.");
                    break;
                case ShopPanel.Reset:
                    Heading("Start a new game?");
                    Text("This replaces your one local save: currency, cultures, discoveries, creatures, furniture, research, upgrade, and incubation progress will reset to preparation night 1.");
                    Space(); Button("Cancel / keep my shop", () => SetPanel(ShopPanel.Pause));
                    Space(); Button("Confirm new game", ConfirmNewGame);
                    break;
                case ShopPanel.CloseShop:
                    Heading("Close for the night?");
                    Text("Stop new arrivals now. You have 3 seconds to finish an accepted checkout; remaining unpaid orders cancel safely. Unsold specimens, cultures and lab work stay with you for the next preparation night.");
                    Space(); Button("Keep trading", () => SetPanel(ShopPanel.Counter));
                    Button("Close today's shop", () => { CloseForNight(); SetPanel(ShopPanel.Counter); });
                    break;
            }
            GUILayout.EndScrollView(); GUILayout.EndArea();
            if (fixedFooter)
            {
                // Keep the exit visible even when panel content needs scrolling.
                GUILayout.BeginArea(new Rect(377,570,526,47));
                Button(dayReportPanel ? "Continue preparation  /  ESC" : "Close  /  ESC", () => SetPanel(ShopPanel.None));
                GUILayout.EndArea();
            }
        }
        void DrawTank()
        {
            Heading(World.SlotName(selectedTank));
            var resident = Model.GetTank(selectedTank);
            if (resident == null)
            {
                Text("This display is empty. Choose a ready or stored creature below.");
                DrawDisplayStockSources();
                return;
            }
            var species = Model.GetSpecies(resident.Value.SpeciesId);
            var reservation = Model.GetReservation(selectedTank);
            Text(reservation.HasValue
                ? species.DisplayName + " is reserved at the agreed " + reservation.Value.Price + " credits. Serve its customer at the front counter."
                : species.DisplayName + " lives here. Open the shop and let a customer choose it.");
            if (Model.Incubator.Phase == IncubatorPhase.Ready)
                Small("Your ready " + Model.GetSpecies(Model.Incubator.SpeciesId).DisplayName + " waits safely in the incubator until a tank is free.");
            DrawDisplayStorageReturn(resident.Value, reservation.HasValue);
            DrawPriceTag(species);
        }
        void DrawPriceTag(SpeciesDefinition species)
        {
            var tag = Model.GetPrice(species.Id).Value;
            Space();
            Small("PRICE TAG / every displayed " + species.DisplayName);
            Heading(tag.Price + " credits" + (tag.Price == tag.Standard ? "  (standard)" : "  (standard " + tag.Standard + ")"));
            Small("HOW EACH CUSTOMER TAKES IT  /  " + Customers.BuyerTypes(tag) + " of " + Customers.ArrivingTypes + " types will buy");
            foreach (var type in Customers.Archetypes)
                if (type.Weight > 0) Text(type.DisplayName + ": " + TypeReactionLabel(type, tag));
            GUILayout.BeginHorizontal();
            PriceButton(species.Id, tag, -5); PriceButton(species.Id, tag, -1);
            PriceButton(species.Id, tag, 1); PriceButton(species.Id, tag, 5);
            GUILayout.EndHorizontal();
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && tag.Price != tag.Standard;
            Button("Use standard price  /  " + tag.Standard + " credits", () => SetSpeciesPrice(species.Id, tag.Standard));
            GUI.enabled = enabled;
            Small("Allowed " + tag.Minimum + "-" + tag.Maximum + " credits. A customer who already reserved pays the price they agreed to; a new tag applies to the next customer.");
            Small(ReputationPriceNote() + " Previews include these bonuses; budget limits still apply.");
        }
        string TypeReactionLabel(CustomerArchetype type, PriceView tag)
        {
            var reaction = Customers.Preview(type, tag);
            string label = reaction == PriceReaction.TooExpensive && type.OverBudget(tag.Price)
                ? "Over budget (" + type.Budget + "), won't buy" : ReactionLabel(reaction);
            return type.Likes(tag.SpeciesId) ? label + "  (collects these)" : label;
        }
        void PriceButton(string speciesId, PriceView tag, int delta)
        {
            int target = Mathf.Clamp(tag.Price + delta, tag.Minimum, tag.Maximum);
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && target != tag.Price;
            Button((delta > 0 ? "+" : "-") + Mathf.Abs(delta), () => SetSpeciesPrice(speciesId, target));
            GUI.enabled = enabled;
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
            { Text(Model.GetSpecies(inc.SpeciesId).DisplayName + " is ready!"); Text("Use E at an empty front display or a lab rack to store your creature and free the incubator."); return; }
            Text("Add exact integer doses. One recipe makes one specimen.");
            Heading(inc.Bloom + " BLOOM  +  " + inc.Flow + " FLOW" + (Model.SparkResearchUnlocked ? "  +  " + inc.Spark + " SPARK" : "") + (Model.MistResearchUnlocked ? "  +  " + inc.Mist + " MIST" : ""));
            GUILayout.BeginHorizontal();
            Button("+ Bloom", () => Perform(Model.Dose(CultureKind.Bloom)));
            Button("+ Flow", () => Perform(Model.Dose(CultureKind.Flow)));
            if (Model.SparkResearchUnlocked) Button("+ Spark", () => Perform(Model.Dose(CultureKind.Spark)));
            if (Model.MistResearchUnlocked) Button("+ Mist", () => Perform(Model.Dose(CultureKind.Mist)));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            Button("- Bloom", () => Perform(Model.ReturnDose(CultureKind.Bloom)));
            Button("- Flow", () => Perform(Model.ReturnDose(CultureKind.Flow)));
            if (Model.SparkResearchUnlocked) Button("- Spark", () => Perform(Model.ReturnDose(CultureKind.Spark)));
            if (Model.MistResearchUnlocked) Button("- Mist", () => Perform(Model.ReturnDose(CultureKind.Mist)));
            GUILayout.EndHorizontal();
            Button("Begin incubation  /  " + Config.IncubationSeconds + " sec", () => { if (Perform(Model.BeginIncubation()).Success) SetPanel(ShopPanel.None); });
            Button("Return all doses safely", () => Perform(Model.ClearDoses()));
            Small("Doses reserve room in your siphon until incubation begins. Unsupported mixtures never consume resources.");
            Space(); Small("Siphon full of one culture? Return staged doses above, then release a spare carried unit. Growers replenish for free.");
            GUILayout.BeginHorizontal();
            Button("Release 1 Bloom", () => Perform(Model.ReleaseCulture(CultureKind.Bloom)));
            Button("Release 1 Flow", () => Perform(Model.ReleaseCulture(CultureKind.Flow)));
            if (Model.SparkResearchUnlocked) Button("Release 1 Spark", () => Perform(Model.ReleaseCulture(CultureKind.Spark)));
            if (Model.MistResearchUnlocked) Button("Release 1 Mist", () => Perform(Model.ReleaseCulture(CultureKind.Mist)));
            GUILayout.EndHorizontal();
            Space(); Small("LAB NOTES: A balanced pair brings a Pondlet. More Bloom makes a rounder body; more Flow makes a longer tail.");
            if (Model.SparkResearchUnlocked) Small("SPARK CLUE: A balanced Bloom/Flow pair needs one Spark.");
            if (Model.MistResearchUnlocked) Small("AIRFIN CLUE: A balanced Bloom/Flow pair needs one Mist.");
            foreach (var species in Config.Species)
                if (Model.IsDiscovered(species.Id)) Button("Prepare " + species.DisplayName + "  (" + RecipeText(species, true) + ")", () => Perform(Model.UseKnownRecipe(species.Id)));
        }
        void DrawCounter()
        {
            Heading("The sales counter");
            if (Cycle.Phase == ShopDayPhase.Night) Button("Arrange rooms / work tables", () => OpenPlacementMenu());
            bool night = Cycle.Phase == ShopDayPhase.Night;
            if (night) Text("Preparation night for day " + Cycle.DayNumber + ". Stock your front tanks, then open when ready.");
            else
            {
                if (Cycle.Phase == ShopDayPhase.Closing) Text("Closing / " + ClockText());
                DrawCustomerOrder();
                Space(); Small("DISPLAY STOCK");
            }
            foreach (var id in Model.TankIds)
            {
                var fish = Model.GetTank(id);
                if (fish == null) { Small(World.SlotName(id) + " / empty"); continue; }
                var species = Model.GetSpecies(fish.Value.SpeciesId);
                var reserved = Model.GetReservation(id);
                var tag = Model.GetPrice(species.Id).Value;
                Small((World.SlotName(id) + ": ") + species.DisplayName + " / " + (reserved.HasValue
                    ? "RESERVED at the agreed " + reserved.Value.Price + " credits"
                    : tag.Price + " credits (" + Customers.BuyerTypes(tag) + " of " + Customers.ArrivingTypes + " customer types buy) / available"));
            }
            Small("Change a price tag with E at its display tank.");
            Space();
            if (Cycle.Phase == ShopDayPhase.Open)
            {
                Small("Keep the displays stocked. Customers leave if there is nothing available, the price is too high, or checkout takes too long.");
                Button("Close for the night...", () => SetPanel(ShopPanel.CloseShop));
            }
            else if (night)
            {
                Small("An 8-minute trading day starts when you open. You can close early; empty stock never traps you in a day.");
                Button("Open for day " + Cycle.DayNumber, () => OpenForDay());
                var lastReport = LastReportForDisplay();
                if (lastReport != null) Button("Review day " + lastReport.dayNumber + " results", () => ShowDayReport());
            }
            else Small("No new arrivals. Unpaid creatures stay in their tanks when the night begins.");
            GUILayout.Space(8);
            Small(ReputationLine() + " / arrival gap " + Customers.NextArrivalSeconds.ToString("0.##") + "s while space is free (max 2 visitors).");
            Small(ReputationPriceNote());
        }
        TradeDaySave LastReportForDisplay()
        {
            int day = Reports == null ? 0 : Reports.LastDayNumber;
            // Last is a defensive snapshot. Copy only when its owner or finalized day changes,
            // instead of allocating a fresh report for every IMGUI layout/repaint event.
            if (!ReferenceEquals(reportDisplayLedger, Reports) || reportDisplayDay != day)
            {
                reportDisplayLedger = Reports;
                reportDisplayDay = day;
                reportDisplaySnapshot = day == 0 ? null : Reports.Last;
            }
            return reportDisplaySnapshot;
        }
        void DrawDayReport()
        {
            var report = LastReportForDisplay();
            if (report == null)
            {
                Heading("Day results");
                Text("There is no completed trading day to report yet. Prepare your displays, then open at the counter.");
                return;
            }
            Heading("Day " + report.dayNumber + " results");
            if (report.partial) Small("PARTIAL DAY / recorded since save upgrade; earlier trade excluded.");
            GUILayout.Space(8);
            GUILayout.Label("Sales income: " + report.revenue + " credits", statStyle);
            Text("Specimens sold: " + report.sales + "    /    Visitors: " + report.visitors);
            Small("Cash now: " + Model.Currency + " credits    /    Wages: 0 (no employees)");
            Small("Shop reputation now: " + Reputation.Points + " / 100 / " + Reputation.TierName);
            GUILayout.Space(8);
            Heading("Display stock");
            Text((report.partial ? "At recording start: " : "At opening: ") + report.stockedAtOpen +
                "    /    Restocked: " + report.restocked + "    /    At close: " + report.stockedAtClose);
            GUILayout.Space(8);
            long missed = (long)report.noStock + report.impatient + report.closed + report.interrupted +
                report.pathProblems + report.tooExpensive + report.slapped;
            Heading("Left without a purchase: " + missed);
            Small("Too expensive: " + report.tooExpensive + "    /    No stock: " + report.noStock + "    /    Patience ran out: " + report.impatient);
            Small("Closing: " + report.closed + "    /    Interrupted visits: " + report.interrupted + "    /    Route problems: " + report.pathProblems);
            Small("Slapped departures: " + report.slapped);
            GUILayout.Space(8);
            Heading("Prepare for the next day");
            Text(ReportPreparationTip(report));
            Small(Model.MistResearchUnlocked ? "Free Bloom, Flow, Spark and Mist. Untimed preparation; your lab keeps growing." : Model.SparkResearchUnlocked ? "Free Bloom, Flow and Spark. Untimed preparation; your lab keeps growing." : "Free Bloom + Flow. Untimed preparation; your lab keeps growing.");
        }
        string ReportPreparationTip(TradeDaySave report)
        {
            int empty = 0;
            foreach (string tank in Model.TankIds) if (!Model.GetTank(tank).HasValue) empty++;
            var incubator = Model.Incubator;
            if (incubator.Phase == IncubatorPhase.Ready)
            {
                if (empty > 0)
                    return "Your " + Model.GetSpecies(incubator.SpeciesId).DisplayName + " is ready. Press E at an empty front tank to stock it before opening.";
                return "All display slots are stocked. Keep the finished specimen in its incubator until a sale frees a tank.";
            }
            if (empty > 0)
            {
                string tankText = empty == 1 ? "Fill the empty display" : "Fill your empty displays";
                if (incubator.Phase == IncubatorPhase.Incubating)
                    return tankText + " before opening. While your specimen grows, collect free cultures for the next one.";
                return tankText + " before opening. One Bloom + one Flow makes a Pondlet; grow and transfer one specimen at a time.";
            }
            if (report.tooExpensive > 0 && report.tooExpensive >= report.impatient && report.tooExpensive >= report.noStock)
                return (report.tooExpensive == 1 ? "A customer" : report.tooExpensive + " customers") +
                    " refused your prices. Press E at a display to lower its tag: \"Pricey\" still sells, \"Too expensive\" does not.";
            if (report.slapped > 0) return "Slapped customers leave without paying. Serve at the counter with E; the slap key is a separate optional gag.";
            if (report.impatient > 0 && report.impatient >= report.noStock)
                return "Displays are ready. To avoid impatient departures, return to the counter promptly when a creature is reserved.";
            if (report.noStock > 0)
                return "Displays are ready. To avoid stockouts, start another specimen before opening and refill a tank after each sale.";
            if (!Model.UpgradeOwned && Model.Currency >= Config.UpgradeCost)
                return "Buy the " + Config.UpgradeCost + "-credit siphon upgrade in the back lab for a larger chamber and faster collection.";
            if (incubator.Phase == IncubatorPhase.Incubating)
                return "Your displays are ready and the next specimen is growing. Open at the front counter when you are ready to serve.";
            return "Your displays are ready. Start the next specimen in the back lab, then open at the front counter when you are ready.";
        }
        void DrawCustomerOrder()
        {
            var visit = Customers.NextToServe;
            if (visit == null) { Text(Customers.Status); return; }
            if (visit.Phase == CustomerPhase.Queued)
            {
                var species = Model.GetSpecies(visit.SpeciesId);
                Heading((species == null ? "Creature" : species.DisplayName) + " / " + visit.Price + " credits agreed");
                Text(visit.ArchetypeName + " waiting / " + Mathf.CeilToInt(visit.QueueSecondsRemaining) + " seconds patience / they said: " + ReactionLabel(visit.Reaction));
                bool enabled = GUI.enabled;
                GUI.enabled = enabled && IsAtCounter;
                Button("Serve customer  /  +" + visit.Price + " credits", () => ServeCustomer());
                GUI.enabled = enabled;
                if (!IsAtCounter) Small("Return to the physical front counter to complete checkout.");
            }
            else
            {
                Text(Customers.Status);
                if (visit.Phase == CustomerPhase.ToCounter)
                {
                    var species = Model.GetSpecies(visit.SpeciesId);
                    Small("Reserved: " + (species == null ? "creature" : species.DisplayName) + " / " + visit.Price + " credits agreed (" + ReactionLabel(visit.Reaction) + ")");
                }
            }
            foreach (var other in Customers.Visits)
                if (other != visit) Small("Also here: " + VisitorLine(other));
        }
        string VisitorLine(CustomerVisit visit)
        {
            var species = Model.GetSpecies(visit.SpeciesId);
            switch (visit.Phase)
            {
                case CustomerPhase.Queued: return visit.ArchetypeName + " in line, " + (species == null ? "creature" : species.DisplayName) + " for " + visit.Price + " / " + Mathf.CeilToInt(visit.QueueSecondsRemaining) + "s patience";
                case CustomerPhase.ToCounter: return visit.ArchetypeName + " bringing a " + (species == null ? "creature" : species.DisplayName) + " (" + visit.Price + " agreed) to the counter";
                case CustomerPhase.Paid: return visit.ArchetypeName + ", just paid";
                case CustomerPhase.Leaving: return visit.ArchetypeName + ", leaving";
                default: return visit.ArchetypeName + ", browsing";
            }
        }
        void DrawUpgrade()
        {
            DrawSparkResearch();
            Space(); Heading("Siphon equipment");
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
            if (Cycle.Phase == ShopDayPhase.Night) Button("Arrange rooms / work tables", () => OpenPlacementMenu());
            foreach (var species in Config.Species)
            {
                Space();
                if (Model.IsDiscovered(species.Id))
                {
                    var tag = Model.GetPrice(species.Id).Value;
                    Heading(species.DisplayName + " / " + species.FamilyId);
                    Text(RecipeText(species) + "  /  standard " + tag.Standard + " credits" +
                        (tag.Price == tag.Standard ? "" : "  /  your tag " + tag.Price));
                }
                else if (species.Mist > 0)
                { Heading(Model.MistResearchUnlocked ? "Unknown Airfin specimen" : "Airfin research locked"); Small(Model.MistResearchUnlocked ? "One Bloom + one Flow + one Mist. Use the incubator / 10 seconds." : "Discover Lanternfin, place the conditioning vat, then research Mist there for " + Config.MistResearchCost + " credits."); }
                else if (species.Spark > 0)
                { Heading(Model.SparkResearchUnlocked ? "Unknown Spark specimen" : "Spark research locked"); Small(Model.SparkResearchUnlocked ? "A balanced Bloom/Flow pair needs one Spark. Use the incubator / 10 seconds." : "Discover the three starter species, then research Spark at the back lab bench for " + Config.SparkResearchCost + " credits."); }
                else {Heading("Unknown specimen"); Small(species.Id == "pondlet" ? "Begin with a balanced pair." : species.Id == "bubblebelly" ? "A little more Bloom may round things out." : "A little more Flow may stretch a tail.");}
            }
            Space(); Small("All current recipes use the incubator / " + Config.IncubationSeconds + " seconds. Cultures replenish for free during active days and nights.");
            Space(); Small("Known recipes can be prepared in one click at the incubator, using cultures you have collected.");
            Space(); Heading("Cartoon slap");
            Text("Tap " + SlapKey + " while aiming at a nearby unpaid customer, with every panel closed. This is separate from E service. They protest and leave; their unpaid creature stays in its tank and reputation drops by 1.");
            Small("Three-second active-time cooldown; one reaction per visitor. Paid customers and departing visitors cannot be slapped. No money reward. Remap the key and adjust humor volume or reduced effects in Pause.");
            Space(); Heading("Your shop's reputation");
            Text(ReputationLine());
            Text(Reputation.TierName);
            Small(Reputation.Tier < 2 ? "Next tier at " + Reputation.NextThreshold + " points." : "Highest tier reached. Reputation is capped at 100.");
            Text("Each completed sale earns 2 points. A visitor who leaves because prices are too high, patience runs out, or you slap them costs 1 point. A refusal followed by a purchase counts as a sale.");
            Small("No reputation loss for empty displays, closing, reloads, or route problems. Reputation stays between 0 and 100; it changes per visit, not at day end.");
            Text("Reputation and store appeal bring arrivals closer together when there is room: currently " + Customers.NextArrivalSeconds.ToString("0.##") + " seconds between arrivals, with at most two visitors in the store.");
            Text(ReputationPriceNote());
            Small("The bonus is added to each customer's usual limit; for example, 150% plus 10% of standard becomes 160%. Their budget still caps the purchase. Existing visitors keep the limits they arrived with; agreed prices stay fixed.");
            Small("At 10 points: 5s arrival gap, +10% of standard to price limits, +2 credits to budgets. At 30 points: 4s, +20%, +4 credits. Bonuses stop growing at the highest tier. These values are provisional.");
            DrawAppealDetails();
            Space(); Heading("Your customers");
            Small("Current limits for new arrivals, including reputation and appeal. Up to two visit at once and wait in line at the counter, first come, first served.");
            foreach (var type in Customers.Archetypes)
                if (type.Weight > 0) Text(type.DisplayName + ": " + CustomerNote(type));
        }
        static string CustomerNote(CustomerArchetype type)
        {
            switch (type.Id)
            {
                case CustomerArchetypes.Bargain: return "spends at most " + type.Budget + " credits and loves a discount. Very patient (" + type.PatienceSeconds + "s).";
                case CustomerArchetypes.Collector: return "looks for Bubblebellies and Ribbonfins first; pays up to " + type.FavoriteTolerancePercent / 100f + "x their standard price, or " + type.TolerancePercent / 100f + "x for other species. Budget: " + type.Budget + " credits.";
                case CustomerArchetypes.Premium: return "pays up to " + type.TolerancePercent / 100f + "x standard, within a " + type.Budget + "-credit budget; waits only " + type.PatienceSeconds + "s at the counter.";
                default: return "pays up to " + type.TolerancePercent / 100f + "x standard, within a " + type.Budget + "-credit budget; waits " + type.PatienceSeconds + "s at the counter.";
            }
        }
        string ReputationLine() => "Reputation " + Reputation.Points + " / 100  |  Appeal " + Appeal.Points + " / 30";
        string ReputationPriceNote() => "New visitors: +" + (Reputation.PremiumPercent + Appeal.ToleranceBonusPercent) + "% of standard added to price limits; +" + (Reputation.BudgetBonus + Appeal.BudgetBonus) + " credits to budgets.";
        void Heading(string text) => GUILayout.Label(text, headingStyle);
        void Text(string text) => GUILayout.Label(text, bodyStyle);
        void Small(string text) => GUILayout.Label(text, smallStyle);
        void Space() => GUILayout.Space(12);
        void Button(string text, System.Action action) { if (GUILayout.Button(text, buttonStyle, GUILayout.MinHeight(38))) action(); }
    }
}
