using System;
using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class ShopWorld
    {
        ShopDayPhase roomCurrentPhase, roomTargetPhase;
        FxRoomLightPose roomLightFrom, roomLightTarget;
        float roomLightAge = FxRoomAtmosphereMotion.LightTransitionSeconds, roomClock, roomVaporWait = 1.3f;
        bool roomLightsInitialized, roomWorking;
        FxOwnerToken roomAtmosphereOwner;
        Transform roomAtmosphereSource;
        int roomEmissionCount, roomFrame;
        float roomRealtime;
        public FxOwnerToken RoomAtmosphereOwner => roomAtmosphereOwner;
        public Transform RoomAtmosphereSource => roomAtmosphereSource;
        public FxRoomAtmosphereSnapshot RoomAtmosphereSnapshot => new FxRoomAtmosphereSnapshot(roomCurrentPhase, roomTargetPhase,
            FxRoomAtmosphereMotion.LightBlend(roomLightAge), roomClock, roomWorking, roomEmissionCount, fxPolicy, roomFrame, roomRealtime, roomWorking && fxPool != null && fxPool.ActionLimit < 64);

        void CheckRoomPhase(ShopDayPhase phase)
        {
            if (phase != ShopDayPhase.Night && phase != ShopDayPhase.Open && phase != ShopDayPhase.Closing)
                throw new ArgumentOutOfRangeException(nameof(phase));
            if (!keyLight || !storeFillLight || !labFillLight)
                throw new InvalidOperationException("Build the shop before applying its day phase.");
        }

        void RestoreRoomPhase(ShopDayPhase phase)
        {
            CheckRoomPhase(phase);
            roomCurrentPhase = roomTargetPhase = phase;
            roomLightFrom = roomLightTarget = FxRoomAtmosphereMotion.Lights(phase);
            roomLightAge = FxRoomAtmosphereMotion.LightTransitionSeconds;
            roomLightsInitialized = true;
            ApplyRoomLights(roomLightTarget);
        }

        public void TransitionDayPhase(ShopDayPhase phase)
        {
            CheckRoomPhase(phase);
            if (!roomLightsInitialized) { RestoreRoomPhase(phase); return; }
            if (phase == roomTargetPhase) return;
            roomLightFrom = new FxRoomLightPose(keyLight.color, storeFillLight.color, labFillLight.color,
                keyLight.intensity, storeFillLight.intensity, labFillLight.intensity);
            roomTargetPhase = phase;
            roomLightTarget = FxRoomAtmosphereMotion.Lights(phase);
            roomLightAge = 0;
        }

        void ApplyRoomLights(FxRoomLightPose pose)
        {
            keyLight.color = pose.KeyColor; keyLight.intensity = pose.KeyIntensity;
            storeFillLight.color = pose.ShopColor; storeFillLight.intensity = pose.ShopIntensity;
            labFillLight.color = pose.LabColor; labFillLight.intensity = pose.LabIntensity;
            RefreshWingLights(); RefreshLabWingLights();
        }

        void CancelRoomAtmosphere()
        {
            fxPool?.Cancel(roomAtmosphereOwner);
            fxCues?.Cancel(roomAtmosphereOwner);
            roomWorking = false; roomAtmosphereSource = null; roomVaporWait = 1.3f;
        }

        void TickRoomAtmosphere(float dt)
        {
            if (float.IsNaN(dt) || float.IsInfinity(dt) || dt <= 0) return;
            roomFrame = Time.frameCount; roomRealtime = Time.realtimeSinceStartup;
            // Persistent lighting follows the session clock; pause never advances this owner.
            if (roomLightsInitialized && roomLightAge < FxRoomAtmosphereMotion.LightTransitionSeconds)
            {
                roomLightAge = Mathf.Min(FxRoomAtmosphereMotion.LightTransitionSeconds, roomLightAge + dt);
                ApplyRoomLights(FxRoomAtmosphereMotion.BlendLights(roomLightFrom, roomLightTarget, FxRoomAtmosphereMotion.LightBlend(roomLightAge)));
                if (roomLightAge >= FxRoomAtmosphereMotion.LightTransitionSeconds) roomCurrentPhase = roomTargetPhase;
            }
            bool working = fxEnabled && fxPolicy == FxViewPolicy.Visible && lastModel != null &&
                lastModel.Incubator.Phase == IncubatorPhase.Incubating && fxCues != null && fxPool != null &&
                TryGetLabFacility(ShopLayout.IncubatorFacilityId, out var source) && source && source.gameObject.activeInHierarchy;
            if (!working)
            {
                if (roomWorking || roomAtmosphereSource) CancelRoomAtmosphere();
                return;
            }
            // Resolve once per activation; no second writer for machine water, tubes or creature motion.
            TryGetLabFacility(ShopLayout.IncubatorFacilityId, out var station);
            if (!roomWorking || roomAtmosphereSource != station || !fxCues.IsValid(roomAtmosphereOwner))
            {
                CancelRoomAtmosphere();
                roomAtmosphereSource = station;
                roomAtmosphereOwner = BindFxOwner("room-vent", ShopLayout.IncubatorFacilityId, station);
                roomWorking = fxCues.IsValid(roomAtmosphereOwner);
                if (!roomWorking) return;
                fxCues.SetState(roomAtmosphereOwner, FxSteadyState.Working);
            }
            roomClock += Mathf.Min(dt, .5f);
            // Low keeps the two existing machine ambient owners; this decorative layer yields entirely.
            if (fxPool.ActionLimit < 64) return;
            roomVaporWait -= Mathf.Min(dt, .5f);
            if (roomVaporWait > 0) return;
            roomVaporWait = fxReducedMotion ? 2.6f : 1.3f;
            int count = fxReducedMotion ? 1 : 2;
            for (int i = 0; i < count; i++)
            {
                // Reuse the authored crown vent proven by FX16; never emit inside the glass cavity.
                Vector3 origin = new Vector3(i == 0 ? 0 : .035f, 2.32f, .027f);
                var puff = FxParticleRequest.Create(FxParticleShape.Puff, new Color(.74f, .91f, .87f, .43f));
                puff.Lifetime = fxReducedMotion ? .45f : .60f;
                puff.Size = i == 0 ? .075f : .055f; puff.EndSize = fxReducedMotion ? puff.Size : .11f;
                if (!fxReducedMotion)
                {
                    puff.Motion = FxParticleMotion.Curve;
                    puff.End = new Vector3(i == 0 ? -.025f : .035f, .12f, .015f);
                    puff.Bend = new Vector3(i == 0 ? .02f : -.02f, .02f, 0);
                }
                if (fxPool.EmitParticle(fxCues, roomAtmosphereOwner, station, origin, puff, false)) roomEmissionCount++;
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public Light RoomLightForValidation(int index)
        {
            if (index == 0) return keyLight;
            if (index == 1) return storeFillLight;
            if (index == 2) return labFillLight;
            if (index >= 3 && index < 5) return wingLights[index - 3];
            if (index >= 5 && index < 7) return labWingLights[index - 5];
            return null;
        }
        public void PreviewRoomAtmosphereForValidation(float blend)
        {
            if (roomLightsInitialized) ApplyRoomLights(FxRoomAtmosphereMotion.BlendLights(roomLightFrom, roomLightTarget, blend));
        }
#endif
    }
}
