using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class ShopSession
    {
        public float AmbienceVolume = FxAudioMix.DefaultAmbienceVolume;
        ShopRoomDoor audioDoor;
        bool audioDoorKnown, audioDoorOpening;
        void TickSessionAudio()
        {
            if (!sound || !World || !Player || Model == null) return;
            var door = World.LabDoor;
            bool suspended = !fxHasFocus || Paused || PlacementActive || Panel == ShopPanel.Arrange || Panel == ShopPanel.Presentation;
            Vector3 listener = Player.transform.position;
            Station water = null, machine = null;
            float waterDistance = float.PositiveInfinity, machineDistance = float.PositiveInfinity;
            for (int i = 0; i < World.Stations.Count; i++)
            {
                var station = World.Stations[i];
                if (!station || !station.isActiveAndEnabled) continue;
                float distance = (station.transform.position - listener).sqrMagnitude;
                bool tank = (station.Kind == StationKind.TankA || station.Kind == StationKind.TankB || station.Kind == StationKind.Display) && Model.HasTank(station.TankId);
                if (tank && distance < waterDistance) { water = station; waterDistance = distance; }
                bool working = station.Kind == StationKind.Incubator && Model.Incubator.Phase == IncubatorPhase.Incubating;
                if (station.Kind == StationKind.Conditioning)
                {
                    var vat = Model.GetConditioner(station.ConditionerItemId);
                    working = vat.HasValue && vat.Value.Phase == ConditionerPhase.Processing;
                }
                if (TryStationCulture(station, out var culture) && Model.IsCultureUnlocked(culture))
                {
                    var grower = Model.GetGrower(culture); working |= grower.Units < grower.Capacity;
                }
                if (working && distance < machineDistance) { machine = station; machineDistance = distance; }
            }
            float doorZ = door ? door.transform.position.z : .5f;
            sound.SetMixState(listener, doorZ, door ? door.OpenFraction : 0, AmbienceVolume,
                water ? water.TankId : null, water ? water.transform.position : Vector3.zero,
                machine ? (machine.ConditionerItemId ?? machine.FacilityId) : null,
                machine ? machine.transform.position : Vector3.zero, suspended);
            if (!door || suspended) { audioDoorKnown = false; audioDoor = door; return; }
            bool opening = door.MotionVelocity > 0;
            bool moving = Mathf.Abs(door.MotionVelocity) > .001f;
            if (audioDoorKnown && audioDoor == door && moving && opening != audioDoorOpening)
                sound.DoorMotion(.35f * Mathf.Clamp01(AmbienceVolume) * FxAudioMix.Attenuation(listener, door.transform.position, doorZ, door.OpenFraction));
            if (!audioDoorKnown || moving) audioDoorOpening = opening;
            audioDoorKnown = true; audioDoor = door;
        }
        void CancelSessionAudio()
        {
            audioDoorKnown = false; audioDoor = null; sound?.StopMixAudio();
        }
        uint NativeUiAudioSequence() => sound ? sound.ServiceRequestSequence : 0;
        void NativeUiNavigationAudio(uint sequence, ShopPanel previousPanel)
        {
            if (sound && fxHasFocus && previousPanel != Panel && !PlacementActive &&
                Panel != ShopPanel.Arrange && Panel != ShopPanel.Presentation) sound.UiNavigation(sequence);
        }
    }
}
