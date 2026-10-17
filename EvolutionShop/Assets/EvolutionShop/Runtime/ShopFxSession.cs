namespace EvolutionShop
{
    public sealed partial class ShopSession
    {
        // Development-only frozen particle fixture. Ordinary play and every other diagnostic keep normal simulation.
        bool FxVocabularyFrozen
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return ValidationMode && FreezeFxVocabularyForValidation;
#else
                return false;
#endif
            }
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public bool FreezeFxVocabularyForValidation { get; set; }
#endif
        public void PresentCustomerPrice(CustomerActor actor,FxCustomerReaction reaction)
        {
            if(World && fxHasFocus && !Paused && !PlacementActive && Panel==ShopPanel.None)
                World.PresentCustomerReaction(actor,reaction,NextFxSequence());
        }
        void PresentCustomerCheckout(CustomerVisit visit,bool saved,FxCueOrigin origin)
        {
            if(!World || visit==null)return;
            var actor=Customers.ActorOf(visit);
            if(!saved){if(actor)World.CancelFxOwner(actor.CustomerFxOwner);return;}
            if(visit.Phase==CustomerPhase.Paid && visit.Outcome==CustomerOutcome.Sold &&
                fxHasFocus && !Paused && !PlacementActive && (Panel==ShopPanel.None || Panel==ShopPanel.Counter))
                World.PresentCustomerReaction(actor,FxCustomerReaction.Checkout,NextFxSequence(),origin);
        }
        internal void PresentWorkerHandling(ShopCharacterVisual actor,string jobId)
        {PresentWorkerResult(actor,FxWorkerEvent.Pickup,jobId,true);}
        internal void PresentWorkerResult(ShopCharacterVisual actor,FxWorkerEvent kind,string jobId,bool saved)
        {
            if(!World||!actor)return;
            if(!saved){World.CancelFxOwner(actor.WorkerFxOwner);return;}
            if(fxHasFocus&&!Paused&&!PlacementActive&&Panel!=ShopPanel.Arrange)
                World.PresentWorkerEvent(actor,kind,jobId,NextFxSequence());
        }
        uint fxSequence;
        bool fxHasFocus = true;
        uint NextFxSequence()
        {
            fxSequence++;
            if (fxSequence == 0) { World?.ResetFxPresentation(); fxSequence = 1; }
            return fxSequence;
        }
        void PresentFxResult(ActionResult result, bool saved, FxCueId successCue, FxCueOrigin origin = FxCueOrigin.Manual, CultureKind culture = CultureKind.Bloom, string destination = null)
        {
            if (!World) return;
            if (result.Success && !saved) {
                World.CancelFxTransient();
                if (successCue == FxCueId.CollectedUnit) World.BlockCollectionUntilRelease();
                return;
            }
            // Only opted-in manual actions bind the hero proof cues; ordinary worker feedback is preserved.
            if (successCue == FxCueId.None) return;
            if (result.Success && saved)
            {
                if (successCue == FxCueId.CollectedUnit && (culture == CultureKind.Bloom || culture == CultureKind.Flow)) sound.LabCollect();
                else if (successCue == FxCueId.DoseCommitted || successCue == FxCueId.DoseReturned || successCue == FxCueId.IncubationStarted) sound.LabFeedback(true, successCue);
            }
            uint sequence = NextFxSequence();
            World.TryFxCue(result.Success ? successCue : FxCueId.HandRefused, sequence, origin, culture);
            // Saved habitat transfers are presented once by the publication adapter.
        }
        Station PlayerResultStation(FxPlayerInteraction kind, string destination)
        {
            if (!World || !fxHasFocus || Paused || PlacementActive || string.IsNullOrEmpty(destination)) return null;
            foreach (var station in World.Stations) {
                bool matches = kind == FxPlayerInteraction.Transfer ? station.TankId == selectedTank :
                    kind == FxPlayerInteraction.Rack ? station.StorageItemId == selectedRack : station.TankId == destination;
                if (matches && AtStockStation(station)) return station;
            }
            return null;
        }
        void PresentPlayerResult(ActionResult result, bool saved, FxPlayerInteraction kind, string destination)
        {
            if (!World || kind == FxPlayerInteraction.None) return;
            if (result.Success && !saved) { World.CancelFxTransient(); return; }
            var station = PlayerResultStation(kind, destination);
            if (!station) return;
            uint sequence = NextFxSequence();
            World.PresentPlayerInteraction(result.Success ? kind : FxPlayerInteraction.Refused, station, destination, sequence);
            // Habitat response uses the exact source/destination snapshot in PerformWithFx.
        }
        void PresentPlayerUpgradeRefusal()
        {
            if (!World || !fxHasFocus || Panel != ShopPanel.Upgrade) return;
            foreach (var station in World.Stations)
                if (station.Kind == StationKind.Upgrade && AtStockStation(station)) {
                    World.PresentPlayerInteraction(FxPlayerInteraction.Refused, station, null, NextFxSequence()); return;
                }
        }
        void PresentPlayerInteract(Station station)
        {
            if (World && fxHasFocus && AtStockStation(station))
                World.PresentPlayerInteraction(FxPlayerInteraction.Interact, station, null, NextFxSequence());
        }
        void ApplyFxViewPolicy()
        {
            if (!World) return;
            ReconcileLabPresentation();
            World.SetFxViewPolicy(!fxHasFocus ? FxViewPolicy.Unfocused : PlacementActive || Panel == ShopPanel.Arrange ? FxViewPolicy.Arrange :
                Paused ? FxViewPolicy.Paused : Panel != ShopPanel.None ? FxViewPolicy.Modal : FxViewPolicy.Visible);
            if (!fxHasFocus || Paused || PlacementActive || Panel == ShopPanel.Arrange) sound?.StopLabAudio();
            if (World.ResearchFxRemaining <= 0 || !fxHasFocus || Paused || PlacementActive || Panel == ShopPanel.Arrange) sound?.StopResearchAudio();
            if (World.LabDoseRemaining <= 0 || !fxHasFocus || Paused || PlacementActive || (Panel != ShopPanel.None && Panel != ShopPanel.Incubator)) sound?.StopDoseAudio();
            if (World.LabHatchRemaining <= 0 || !fxHasFocus || Paused || PlacementActive || Panel != ShopPanel.None) sound?.StopHatchAudio();
        }
        void ResetSessionFx() { fxSequence = 0; World?.ResetFxPresentation(); sound?.StopResearchAudio(); sound?.StopHatchAudio(); sound?.StopDoseAudio(); }
        void DisableSessionFx() { if (World) { World.SetFxViewPolicy(FxViewPolicy.Unfocused); World.CancelFxTransient(); sound?.StopResearchAudio(); sound?.StopLabAudio(); sound?.StopHatchAudio(); sound?.StopDoseAudio(); } }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public uint FxActionSequenceForValidation => fxSequence;
        public void SetFxFocusForValidation(bool value) { fxHasFocus = value; ApplyFxViewPolicy(); }
#endif
    }
}
