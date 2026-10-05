using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class ShopSession
    {
        GUIStyle titleStyle, headingStyle, bodyStyle, smallStyle, buttonStyle, statStyle, centerStyle, controlsStyle, hudTitleStyle, hudBodyStyle, hudSmallStyle, hudCurrencyStyle, toggleStyle;
        GUIStyle hudLightStyle, hudLightSmallStyle, hudBadgeStyle, hudKeyStyle, hudLargeCurrencyStyle, hudLongDayStyle;
        ShopUiTheme hudTheme;
        readonly GUIContent hudMeasure = new GUIContent();
        readonly Color ink = ShopUiTheme.Paper;
        readonly Color mint = ShopUiTheme.Teal;
        readonly Color hudInk = ShopUiTheme.Ink;
        readonly Color coral = ShopUiTheme.Coral;
        Vector2 panelScroll;
        ShopTradeLedger reportDisplayLedger;
        TradeDaySave reportDisplaySnapshot;
        int reportDisplayDay = -1;

        void Styles()
        {
            if (titleStyle != null) return;
            hudTheme = new ShopUiTheme(GUI.skin);
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold };
            titleStyle.normal.textColor = hudInk;
            headingStyle = new GUIStyle(titleStyle) { fontSize = 20 };
            bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
            bodyStyle.normal.textColor = hudInk;
            smallStyle = new GUIStyle(bodyStyle) { fontSize = 13 };
            smallStyle.normal.textColor = ShopUiTheme.Muted;
            buttonStyle = hudTheme.Button;
            statStyle = new GUIStyle(headingStyle); statStyle.normal.textColor = mint;
            centerStyle = new GUIStyle(bodyStyle) { alignment = TextAnchor.MiddleCenter };
            controlsStyle = new GUIStyle(smallStyle) { alignment = TextAnchor.MiddleCenter, wordWrap = false };
            hudTitleStyle = new GUIStyle(headingStyle) { fontSize = 16, wordWrap = true };
            hudBodyStyle = new GUIStyle(bodyStyle) { fontSize = 14 };
            hudSmallStyle = new GUIStyle(smallStyle) { fontSize = 11, wordWrap = true };
            hudCurrencyStyle = new GUIStyle(statStyle) { fontSize = 22, alignment = TextAnchor.MiddleLeft };
            hudLargeCurrencyStyle = new GUIStyle(hudCurrencyStyle) { fontSize = 17 };
            hudLightStyle = new GUIStyle(hudTitleStyle); hudLightStyle.normal.textColor = ink;
            hudLightSmallStyle = new GUIStyle(hudSmallStyle); hudLightSmallStyle.normal.textColor = new Color(.83f, .9f, .8f);
            hudLongDayStyle = new GUIStyle(hudLightStyle) { fontSize = 10 };
            hudBadgeStyle = new GUIStyle(hudBodyStyle) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            hudKeyStyle = new GUIStyle(hudLightStyle) { alignment = TextAnchor.MiddleCenter, wordWrap = false };
            toggleStyle = new GUIStyle(GUI.skin.toggle);
            toggleStyle.normal.textColor = toggleStyle.hover.textColor = toggleStyle.active.textColor = toggleStyle.focused.textColor = hudInk;
            toggleStyle.onNormal.textColor = toggleStyle.onHover.textColor = toggleStyle.onActive.textColor = toggleStyle.onFocused.textColor = hudInk;
        }

        // Called by the session owner; UI textures/skin are never shared imported assets.
        void DisposeHudTheme()
        {
            hudTheme?.Dispose(); hudTheme = null; titleStyle = null; hudLightControlsStyle = null;
        }

        static void Box(Rect rect, Color color)
        {
            Color before = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = before;
        }
        void HudCard(Rect rect, Color accent)
        {
            hudTheme.Draw(new Rect(rect.x, rect.y + 3, rect.width, rect.height), hudTheme.Shadow);
            hudTheme.Draw(rect, hudTheme.Card);
            // A small stitched tab identifies the card without adding a heavy frame.
            hudTheme.Draw(new Rect(rect.x + 12, rect.y + 1, Mathf.Min(34, rect.width - 24), 4), hudTheme.Accent, accent);
        }
        float HudTextHeight(string text, GUIStyle style, float width)
        {
            hudMeasure.text = text;
            return Mathf.Ceil(style.CalcHeight(hudMeasure, width));
        }
        void OnGUI()
        {
            if (!initialized) return;
            Styles();
            Matrix4x4 previousMatrix = GUI.matrix;
            GUISkin previousSkin = GUI.skin;
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1280 * scale) / 2, (Screen.height - 720 * scale) / 2, 0), Quaternion.identity, Vector3.one * scale);
            GUI.skin = hudTheme.Skin;
            try
            {
                if (Panel == ShopPanel.Presentation) { DrawPresentationView(); return; }
                DrawCompactStatus();
                DrawCultureStatus();
                hudTheme.Draw(new Rect(433, 676, 548, 26), hudTheme.DarkCard);
                GUI.Label(new Rect(443, 678, 528, 21), PlacementActive ? "WASD move   |   R rotate   |   ESC cancel placement" : "WASD move   |   E interact   |   TAB journal   |   ESC pause", hudLightControlsStyle);
                if (Panel == ShopPanel.None && !PlacementActive) DrawFocusPrompt();
                if (Panel != ShopPanel.None) DrawPanel();
                if (PlacementActive) DrawPlacementOverlay();
                DrawHudNotice();
                DrawDiscoveryCard();
                DrawComedyCaption();
                if (BaselinePreview)
                {
                    HudCard(new Rect(430, 646, 562, 22), coral);
                    GUI.Label(new Rect(441, 648, 540, 20), "DEVELOPMENT COMPARISON: BASE SIPHON (F8 TO RESTORE)", hudSmallStyle);
                }
            }
            finally { GUI.skin = previousSkin; GUI.matrix = previousMatrix; }
        }

        GUIStyle hudLightControlsStyle;
        void DrawFocusPrompt()
        {
            hudTheme.Draw(new Rect(635, 355, 10, 10), hudTheme.Shadow);
            hudTheme.Draw(new Rect(637, 357, 6, 6), hudTheme.Accent, ink);
            float hintTop = 654;
            if (Focused != null)
            {
                bool culture = TryStationCulture(Focused, out var focusCulture);
                string hint = culture ? (focusCulture == CultureKind.Spark ? "Hold left mouse to collect  /  E research" : focusCulture == CultureKind.Mist ? "Hold left mouse to collect  /  E vat" : "Hold left mouse to collect") : "Use station";
                float labelHeight = Mathf.Max(21, HudTextHeight(Focused.Label, hudTitleStyle, 320));
                float hintHeight = Mathf.Max(18, HudTextHeight(hint, hudSmallStyle, 320));
                float height = labelHeight + hintHeight + 21;
                hintTop -= height;
                HudCard(new Rect(424, hintTop, 432, height), mint);
                hudTheme.Draw(new Rect(436, hintTop + 12, 44, 37), hudTheme.DarkCard);
                GUI.Label(new Rect(438, hintTop + 12, 40, 37), culture ? "LMB" : "E", hudKeyStyle);
                GUI.Label(new Rect(492, hintTop + 9, 350, labelHeight), Focused.Label, hudTitleStyle);
                GUI.Label(new Rect(492, hintTop + 9 + labelHeight, 350, hintHeight), hint, hudSmallStyle);
                if (HarvestProgress > 0)
                {
                    hudTheme.Draw(new Rect(492, hintTop + height - 7, 348, 4), hudTheme.Inset);
                    hudTheme.Draw(new Rect(492, hintTop + height - 7, 348 * Mathf.Clamp01(HarvestProgress), 4), hudTheme.Accent, mint);
                }
            }
            if (SlapTarget != null)
            {
                string hint = SlapCooldownRemaining > 0 ? "Ready in " + SlapCooldownRemaining.ToString("0.0") + "s" : "Cartoon slap  /  -1 reputation";
                float nameHeight = Mathf.Max(20, HudTextHeight(SlapTarget.ArchetypeName, hudTitleStyle, 300));
                float height = nameHeight + 39;
                float top = hintTop - height - 8;
                HudCard(new Rect(444, top, 392, height), coral);
                hudTheme.Draw(new Rect(456, top + 11, 36, 36), hudTheme.DarkCard);
                GUI.Label(new Rect(457, top + 11, 34, 36), SlapKey.ToString(), hudKeyStyle);
                GUI.Label(new Rect(504, top + 7, 318, nameHeight), SlapTarget.ArchetypeName, hudTitleStyle);
                GUI.Label(new Rect(504, top + 9 + nameHeight, 318, 22), hint, hudSmallStyle);
            }
        }

        float HudObjectiveHeight() => Mathf.Max(34, HudTextHeight(Objective(), hudBodyStyle, 246)) + 41;
        void DrawCompactStatus()
        {
            if (hudLightControlsStyle == null)
            {
                hudLightControlsStyle = new GUIStyle(controlsStyle);
                hudLightControlsStyle.normal.textColor = ink;
            }
            // No persistent card crosses the top-centre view of the room.
            hudTheme.Draw(new Rect(18, 21, 140, 54), hudTheme.Shadow);
            hudTheme.Draw(new Rect(18, 18, 140, 54), hudTheme.DarkCard);
            hudTheme.Icon(new Rect(28, 29, 31, 31), Cycle.Phase == ShopDayPhase.Night ? ShopUiTheme.Symbol.Moon : ShopUiTheme.Symbol.Sun);
            GUI.Label(new Rect(67, 25, 83, 23), "DAY " + Cycle.DayNumber, Cycle.DayNumber > 9999 ? hudLongDayStyle : hudLightStyle);
            GUI.Label(new Rect(67, 49, 83, 16), Paused ? "PAUSED" : Cycle.Phase == ShopDayPhase.Night ? "NIGHT PREP" : Cycle.Phase == ShopDayPhase.Open ? "SHOP OPEN" : "CLOSING", hudLightSmallStyle);
            HudCard(new Rect(168, 18, 128, 54), mint);
            int seconds = Mathf.CeilToInt(Cycle.SecondsRemaining);
            string clock = Cycle.Phase == ShopDayPhase.Night ? "NO RUSH" : (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
            GUI.Label(new Rect(180, 25, 104, 23), clock, hudBadgeStyle);
            GUI.Label(new Rect(179, 49, 106, 18), Cycle.Phase == ShopDayPhase.Night ? "Untimed preparation" : Cycle.Phase == ShopDayPhase.Open ? "Time left to trade" : "Until preparation", hudSmallStyle);

            if (Panel == ShopPanel.None)
            {
                float height = HudObjectiveHeight();
                HudCard(new Rect(18, 86, 278, height), mint);
                hudTheme.Icon(new Rect(30, 97, 20, 20), ShopUiTheme.Symbol.Leaf);
                GUI.Label(new Rect(57, 99, 221, 18), "YOUR NEXT STEP", hudSmallStyle);
                GUI.Label(new Rect(32, 123, 246, height - 40), Objective(), hudBodyStyle);
            }

            HudCard(new Rect(1030, 18, 232, 54), ShopUiTheme.Honey);
            hudTheme.Icon(new Rect(1042, 27, 36, 36), ShopUiTheme.Symbol.Coin);
            GUI.Label(new Rect(1086, 22, 162, 30), Model.Currency.ToString("N0"), Model.Currency > 9999999 ? hudLargeCurrencyStyle : hudCurrencyStyle);
            GUI.Label(new Rect(1088, 49, 160, 18), "SHOP CREDITS", hudSmallStyle);
            HudCard(new Rect(1030, 82, 232, 41), mint);
            hudTheme.Icon(new Rect(1041, 91, 23, 23), ShopUiTheme.Symbol.Star);
            GUI.Label(new Rect(1071, 94, 178, 22), "Rep " + Reputation.Points + "/100   |   Appeal " + Appeal.Points + "/30", hudSmallStyle);

            bool ready = Model.Incubator.Phase == IncubatorPhase.Ready;
            string machine = Model.Incubator.Phase == IncubatorPhase.Incubating ? "Growing / " + Mathf.CeilToInt(Model.Incubator.SecondsRemaining) + "s" : ready ? "Specimen ready!" : "Ready to create";
            HudCard(new Rect(1030, 133, 232, 66), ready ? coral : mint);
            hudTheme.Icon(new Rect(1042, 145, 31, 31), ShopUiTheme.Symbol.Flask);
            GUI.Label(new Rect(1084, 144, 164, 24), machine, hudTitleStyle);
            GUI.Label(new Rect(1084, 172, 164, 18), Model.DiscoveredSpeciesIds.Count + " / " + Config.Species.Length + " discoveries", hudSmallStyle);
            if (Cycle.Phase != ShopDayPhase.Night || Customers.ActiveCount > 0)
            {
                string status = Customers.Status;
                float statusHeight = Mathf.Max(34, HudTextHeight(status, hudBodyStyle, 204));
                HudCard(new Rect(1030, 209, 232, statusHeight + 38), mint);
                GUI.Label(new Rect(1044, 219, 204, 18), "AT THE FRONT COUNTER", hudSmallStyle);
                GUI.Label(new Rect(1044, 240, 204, statusHeight), status, hudBodyStyle);
            }
        }

        void DrawCultureStatus()
        {
            int count = 2 + (Model.SparkResearchUnlocked ? 1 : 0) + (Model.MistResearchUnlocked ? 1 : 0);
            float width = 28 + count * 80;
            int capacity = BaselinePreview ? Config.BaseCapacity : Model.Capacity;
            bool full = Model.ReservedCultureCount >= capacity;
            int staged = Model.Staged(CultureKind.Bloom) + Model.Staged(CultureKind.Flow) + Model.Staged(CultureKind.Spark) + Model.Staged(CultureKind.Mist);
            HudCard(new Rect(18, 612, width, 90), full ? coral : mint);
            GUI.Label(new Rect(31, 622, width - 24, 19), "SIPHON  " + Model.ReservedCultureCount + "/" + capacity + (full ? "  FULL" : "") + "  /  " + staged + " staged", hudSmallStyle);
            DrawCultureCount(new Rect(31, 646, 73, 41), "BLOOM", Model.Available(CultureKind.Bloom), ShopUiTheme.Symbol.Bloom);
            DrawCultureCount(new Rect(111, 646, 73, 41), "FLOW", Model.Available(CultureKind.Flow), ShopUiTheme.Symbol.Flow);
            int next = 2;
            if (Model.SparkResearchUnlocked) DrawCultureCount(new Rect(31 + 80 * next++, 646, 73, 41), "SPARK", Model.Available(CultureKind.Spark), ShopUiTheme.Symbol.Spark);
            if (Model.MistResearchUnlocked) DrawCultureCount(new Rect(31 + 80 * next, 646, 73, 41), "MIST", Model.Available(CultureKind.Mist), ShopUiTheme.Symbol.Mist);
            hudTheme.Draw(new Rect(31, 693, width - 26, 3), hudTheme.Inset);
            float fill = Mathf.Clamp01(Model.ReservedCultureCount / (float)Mathf.Max(1, capacity));
            if (fill > 0) hudTheme.Draw(new Rect(31, 693, (width - 26) * fill, 3), hudTheme.Accent, full ? coral : mint);
        }
        void DrawCultureCount(Rect rect, string label, int amount, ShopUiTheme.Symbol symbol)
        {
            hudTheme.Icon(new Rect(rect.x, rect.y, 23, 23), symbol);
            GUI.Label(new Rect(rect.x + 28, rect.y - 3, 42, 26), amount.ToString(), hudCurrencyStyle);
            GUI.Label(new Rect(rect.x + 2, rect.y + 24, rect.width, 18), label, hudSmallStyle);
        }

        void DrawHudNotice()
        {
            if (string.IsNullOrEmpty(SaveError) && (string.IsNullOrEmpty(message) || Time.unscaledTime >= messageUntil)) return;
            string notice = string.IsNullOrEmpty(SaveError) ? message : "PROGRESS NOT SAVED - " + SaveError;
            hudMeasure.text = notice;
            float width = Mathf.Clamp(centerStyle.CalcSize(hudMeasure).x + 44, 230, 600);
            float height = Mathf.Max(38, HudTextHeight(notice, centerStyle, width - 32) + 18);
            float top = Panel != ShopPanel.None ? 35 : Comedy.Active ? 208 : 82;
            float left = (1280 - width) / 2;
            HudCard(new Rect(left, top, width, height), string.IsNullOrEmpty(SaveError) ? mint : coral);
            GUI.Label(new Rect(left + 16, top + 9, width - 32, height - 18), notice, centerStyle);
        }
        void DrawDiscoveryCard()
        {
            if (Panel != ShopPanel.None || Time.unscaledTime >= revealUntil || string.IsNullOrEmpty(revealedSpecies)) return;
            var species = Model.GetSpecies(revealedSpecies);
            string recipe = RecipeText(species);
            float recipeHeight = Mathf.Max(20, HudTextHeight(recipe, hudBodyStyle, 246));
            float nameHeight = Mathf.Max(24, HudTextHeight(species.DisplayName, hudTitleStyle, 246));
            float top = 98 + HudObjectiveHeight();
            HudCard(new Rect(18, top, 278, 40 + nameHeight + recipeHeight), coral);
            GUI.Label(new Rect(32, top + 11, 246, 18), "NEW DISCOVERY / JOURNAL UPDATED", hudSmallStyle);
            GUI.Label(new Rect(32, top + 31, 246, nameHeight), species.DisplayName, hudTitleStyle);
            GUI.Label(new Rect(32, top + 31 + nameHeight, 246, recipeHeight), recipe, hudBodyStyle);
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
            Box(new Rect(0,0,1280,720), new Color(.025f,.075f,.07f,.48f));
            hudTheme.Draw(new Rect(342,112,596,528), hudTheme.Shadow);
            hudTheme.Draw(new Rect(344,106,592,528), hudTheme.DarkCard);
            HudCard(new Rect(350,112,580,518), mint);
            hudTheme.Draw(new Rect(310,127,48,48), hudTheme.DarkCard);
            hudTheme.Icon(new Rect(318,135,32,32), Panel == ShopPanel.Incubator || Panel == ShopPanel.Conditioning || Panel == ShopPanel.Upgrade ? ShopUiTheme.Symbol.Flask : ShopUiTheme.Symbol.Leaf);
            // Content and footer anchors are deliberately retained for established input scenarios.
            GUILayout.BeginArea(new Rect(377,130,526,fixedFooter ? 423 : 477));
            panelScroll = GUILayout.BeginScrollView(panelScroll, GUIStyle.none, GUI.skin.verticalScrollbar);
            switch (Panel)
            {
                case ShopPanel.Welcome:
                    Heading("Welcome to your little shop");
                    Text("Front store: display tanks and sales counter. The automatic lab door opens when you approach and closes behind you. Beyond it: your back lab, with growers, incubator, and upgrade bench.");
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
                    Heading("Shop paused"); Button("View presentation sample", () => SetPanel(ShopPanel.Presentation)); Button("Preview comedy kit", OpenComedyPreview); Text("The day clock and all lab production are paused. Your progress saves automatically.");
                    Space(); Text("Mouse sensitivity: " + Sensitivity.ToString("0.00"));
                    Sensitivity = GUILayout.HorizontalSlider(Sensitivity, 0.25f, 2.5f);
                    Space(); Text("Sound volume: " + Mathf.RoundToInt(Volume * 100) + "%");
                    Volume = GUILayout.HorizontalSlider(Volume, 0, 1); AudioListener.volume = Volume;
                    Space(); Heading("Cartoon slap");
                    Small("Close panels, aim at a nearby unpaid customer and tap " + SlapKey + ". They leave; -1 reputation. Three-second cooldown.");
                    Button(WaitingForSlapKey ? "Press a letter / Escape cancels" : "Change slap key / " + SlapKey, BeginSlapRebind);
                    Text("Humor volume: " + Mathf.RoundToInt(HumorVolume * 100) + "%");
                    HumorVolume = GUILayout.HorizontalSlider(HumorVolume, 0, 1);
                    ReducedGags = GUILayout.Toggle(ReducedGags, "Reduced gags (less motion, longer quiet time)", toggleStyle);
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
                Box(new Rect(377,558,526,1), new Color(.72f,.76f,.64f));
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
        void Button(string text, System.Action action)
        {
            bool primary = text.StartsWith("Enter your shop") || text.StartsWith("Begin incubation") ||
                text.StartsWith("Serve customer") || text.StartsWith("Open for day") || text.StartsWith("Continue preparation") ||
                text.StartsWith("Save and resume") || text.StartsWith("Start conditioning") || text.StartsWith("Research ");
            bool caution = text.StartsWith("Confirm new game") || text.StartsWith("Close today's shop") || text.StartsWith("Save and quit");
            var style = caution ? hudTheme.DangerButton : primary ? hudTheme.PrimaryButton : buttonStyle;
            if (GUILayout.Button(text, style, GUILayout.MinHeight(38))) action();
        }
    }
}
