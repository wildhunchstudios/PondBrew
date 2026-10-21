using UnityEngine;

namespace EvolutionShop
{
    // UX-02: bridge between the session and the UI Toolkit preview. The IMGUI path stays the default until
    // each replacement screen reaches parity (UX.10); then UX-09 removes it.
    public sealed partial class ShopSession
    {
        public ShopUiRoot ToolkitUi { get; private set; }
        public bool ToolkitUiActive => ToolkitUi != null && ToolkitUi.Active;
        Station toolkitStation;

        void InitializeToolkitUi()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            bool requested = Arg("-evolutionToolkitUi") != null;
            // Existing validation routes keep their exact object set; only UI routes create the preview root.
            if (ValidationMode && !requested && Arg("-evolutionToolkitUiSmoke") == null && Arg("-evolutionToolkitHudSmoke") == null &&
                Arg("-evolutionToolkitCardsSmoke") == null && Arg("-evolutionToolkitLabCardsSmoke") == null &&
                Arg("-evolutionToolkitPlanningSmoke") == null && Arg("-evolutionToolkitScreensSmoke") == null && Arg("-evolutionToolkitFeelSmoke") == null) return;
            ToolkitUi = gameObject.AddComponent<ShopUiRoot>();
            ToolkitUi.Initialize(this, requested);
#endif
        }

        /// <summary>Turns the UI Toolkit preview on or off; false when the root is unavailable.</summary>
        public bool SetToolkitUiActive(bool active)
        {
            if (ToolkitUi == null || !ToolkitUi.Ready) return false;
            ToolkitUi.SetActive(active);
            return ToolkitUi.Active == active;
        }

        bool ToolkitOwnsPanel(ShopPanel panel) => ToolkitUi != null && ToolkitUi.OwnsPanel(panel);

        /// <summary>UX-08: the comfort settings the UI feel follows, as the world applies them (FX-20).</summary>
        public bool ToolkitReducedMotion => NativeUiReduced;
        public bool ToolkitLowDetail => World ? World.EffectsLow : EffectsDetail == FxEffectDetail.Low;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Validation only (UX-08 feel route): shows the discovery stamp for a species already found, as its first hatch did.</summary>
        internal void RevealDiscoveryForValidation(string speciesId) { revealedSpecies = speciesId; revealUntil = Time.unscaledTime + 3.5f; }
#endif

        void NoteToolkitStation(Station station) { toolkitStation = station; }

        /// <summary>The station a card for this panel points at: the one just used, else the first of that kind.</summary>
        public Station ToolkitAnchorFor(ShopPanel panel)
        {
            if (toolkitStation && PanelForStation(toolkitStation.Kind) == panel) return toolkitStation;
            if (World == null) return null;
            foreach (var station in World.Stations)
                if (station && PanelForStation(station.Kind) == panel) return station;
            return null;
        }

        static ShopPanel PanelForStation(StationKind kind)
        {
            switch (kind)
            {
                case StationKind.Incubator: return ShopPanel.Incubator;
                case StationKind.TankA: case StationKind.TankB: case StationKind.Display: return ShopPanel.Tank;
                case StationKind.Counter: return ShopPanel.Counter;
                case StationKind.Upgrade: return ShopPanel.Upgrade;
                case StationKind.StorageRack: return ShopPanel.StorageRack;
                case StationKind.Conditioning: return ShopPanel.Conditioning;
                default: return ShopPanel.None;
            }
        }
    }
}
