using UnityEngine;

namespace EvolutionShop
{
    // The session's bridge to the UI Toolkit UI (UX-02 to UX-08). Since UX-09 it is the only UI: every build creates it at start-up
    // and it stays on; the IMGUI path it replaced is gone.
    public sealed partial class ShopSession
    {
        public ShopUiRoot ToolkitUi { get; private set; }
        public bool ToolkitUiActive => ToolkitUi != null && ToolkitUi.Active;
        Station toolkitStation;

        void InitializeToolkitUi()
        {
            ToolkitUi = gameObject.AddComponent<ShopUiRoot>();
            ToolkitUi.Initialize(this);
        }

        bool ToolkitOwnsPanel(ShopPanel panel) => ToolkitUi != null && ToolkitUi.OwnsPanel(panel);

        /// <summary>UX-08: the comfort settings the UI feel follows, as the world applies them (FX-20).</summary>
        public bool ToolkitReducedMotion => UiFeedbackReduced;
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
