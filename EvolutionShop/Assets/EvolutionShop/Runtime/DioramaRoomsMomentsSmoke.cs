#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace EvolutionShop
{
    // AD-D effects (part of -evolutionDioramaRoomsSmoke): the sale (scoop into a bag that ties and hops into the visitor's
    // hand), a real slap (impact star, birds, spinning hat), refusals as grey puffs (the siphon's with its shake), fish
    // bubble trails, the moments' sounds on the existing voices, the comfort modes and frame timings.
    public sealed partial class MilestoneSmoke
    {
        [Serializable] sealed class MomentsReceipt
        {
            public int saleMoments, impactStars, greyPuffs, toolShakes, bubbleTrails, soundCues, flipbookAccents, causticSands, voices;
            public float hopPeak, hatTurnPeak;
            public bool bagTied, birdsSeen;
            public int reducedShakes, reducedTrails, lowParticles, fullParticles;
        }
        readonly MomentsReceipt momentsReceipt = new MomentsReceipt();
        uint roomsSequence = 900000;

        IEnumerator RoomsVisitor(string type, Func<CustomerActor, bool> ready)
        {
            shop.Customers.SendVisitorsAwayForValidation(); yield return null;
            var visit = Spawn(type);
            var actor = shop.Customers.ActorOf(visit);
            yield return WaitFor(() => actor && actor.Phase == CustomerPhase.Browsing, 30, "AD-D visitor browses / " + type);
            shop.Customers.HoldVisitsForValidation = true;
            yield return new WaitForSeconds(.4f);
            Check(ready(actor), "The " + type + " is ready for its moment");
        }

        IEnumerator RoomsMoments(ShopWorld w)
        {
            if (!shop.DioramaLookOn) shop.SetDioramaLook(true);
            SeedStock("bubblebelly", "tank-a"); shop.World.Refresh(shop.Model);
            yield return EnterCounter();
            Check(shop.OpenForDay(), "Open the shop for the moments");
            shop.SetPanel(ShopPanel.None);
            RoomsKey(w, ShopDayPhase.Open, .3f);
            CustomerActor actor = null;
            yield return RoomsVisitor(CustomerArchetypes.Casual, a => (actor = a) && a.DioramaVisitor && a.DioramaVisitor.IsDiorama);
            if (!actor) yield break;
            var figure = actor.DioramaVisitor;
            var away = -actor.transform.forward; away.y = 0;
            actor.transform.rotation = Quaternion.LookRotation(away.normalized) * Quaternion.Euler(0, -25, 0);
            yield return null;
            FaceView(figure, 1.7f, .5f);
            // A refusal: only a small grey puff.
            int puffs = w.DioramaGreyPuffs, accents = w.DioramaFlipbookAccents, cues = w.DioramaSoundCues;
            Check(w.PresentCustomerReaction(actor, FxCustomerReaction.Refused, ++roomsSequence), "The visitor refuses a price");
            Check(w.DioramaGreyPuffs == puffs + 1 && w.DioramaFlipbookAccents > accents && w.DioramaSoundCues == cues + 1, "A refusal is a small grey puff with its sound");
            Check(w.FxPool.ActiveCount == 0, "With the look on, the pool shows no procedural accent for it");
            yield return new WaitForSeconds(.15f);
            yield return DioramaShot("moments-refusal-puff");
            yield return new WaitForSeconds(.9f);
            Check(w.PresentCustomerReaction(actor, FxCustomerReaction.Accepted, ++roomsSequence), "The visitor likes a price");
            yield return new WaitForSeconds(.15f);
            yield return DioramaShot("moments-price-nod");
            yield return new WaitForSeconds(.9f);
            // The sale: the scoop, the bag tying, the hop into the hand, seen in profile (the visitor turned a quarter from the
            // camera, so the bag it takes in front of it is in view).
            actor.transform.rotation = actor.transform.rotation * Quaternion.Euler(0, 75, 0); yield return null;
            int sales = w.DioramaSaleMoments; cues = w.DioramaSoundCues;
            Check(w.PresentCustomerReaction(actor, FxCustomerReaction.Checkout, ++roomsSequence), "The visitor checks out");
            Check(w.DioramaSaleMoments == sales + 1 && w.DioramaSaleBag, "A sale scoops the creature into a bag");
            float start = Time.realtimeSinceStartup; bool shotScoop = false, shotTie = false, shotHop = false, shotHand = false;
            while (Time.realtimeSinceStartup - start < ShopWorld.DioramaSaleSeconds + .3f)
            {
                float t = Time.realtimeSinceStartup - start;
                momentsReceipt.hopPeak = Mathf.Max(momentsReceipt.hopPeak, actor.DioramaHopLift);
                momentsReceipt.bagTied |= w.DioramaSaleBagTied;
                if (!shotScoop && t > .18f) { shotScoop = true; yield return DioramaShot("moments-sale-scoop"); continue; }
                if (!shotTie && t > ShopWorld.DioramaBagTieAt + .08f) { shotTie = true; yield return DioramaShot("moments-sale-tie"); continue; }
                if (!shotHop && actor.DioramaHopLift > .09f) { shotHop = true; yield return DioramaShot("moments-sale-hop"); continue; }
                if (!shotHand && t > 1.4f) { shotHand = true; yield return DioramaShot("moments-sale-in-hand"); continue; }
                yield return null;
            }
            Check(momentsReceipt.bagTied && w.DioramaSoundCues >= cues + 1, "The bag ties, with its sound");
            Check(momentsReceipt.hopPeak > .1f && momentsReceipt.hopPeak <= CustomerActor.DioramaHopHeight + .001f, "The visitor hops as the bag reaches its hand / " + momentsReceipt.hopPeak.ToString("0.000"));
            Check(!w.DioramaSaleBag, "The bag pops away when the moment ends");
            // A real slap through the game's own slap action: the impact star, the birds and the spinning hat.
            shop.Customers.HoldVisitsForValidation = false;
            CustomerActor premium = null;
            yield return RoomsVisitor(CustomerArchetypes.Premium, a => (premium = a) && a.DioramaVisitor && a.DioramaVisitor.HasDioramaHat);
            if (!premium) yield break;
            // Turned from the display to face the room, so the slap is seen from the front: its face, stars, birds and hat.
            var room = -premium.transform.forward; room.y = 0;
            premium.transform.rotation = Quaternion.LookRotation(room.normalized) * Quaternion.Euler(0, -20, 0);
            yield return null;
            FaceView(premium.DioramaVisitor, 1.7f, .35f);
            yield return null;
            int stars = w.DioramaImpactStars; cues = w.DioramaSoundCues;
            Check(shop.TrySlapFocusedCustomer(), "Slap the big spender with the game's own slap");
            start = Time.realtimeSinceStartup; bool shotImpact = false, shotBirds = false;
            while (Time.realtimeSinceStartup - start < 2.2f)
            {
                momentsReceipt.birdsSeen |= premium && premium.DioramaBirdsShown;
                if (premium && premium.DioramaVisitor) momentsReceipt.hatTurnPeak = Mathf.Max(momentsReceipt.hatTurnPeak, premium.DioramaVisitor.DioramaHatTurn);
                if (!shotImpact && w.DioramaImpactStars > stars) { shotImpact = true; yield return new WaitForSeconds(.05f); yield return DioramaShot("moments-slap-impact"); continue; }
                if (shotImpact && !shotBirds && premium && premium.DioramaBirdsShown) { shotBirds = true; yield return DioramaShot("moments-slap-birds"); continue; }
                yield return null;
            }
            Check(w.DioramaImpactStars == stars + 1, "A slap is an impact star");
            Check(momentsReceipt.birdsSeen, "Cartoon birds circle the slapped visitor");
            Check(momentsReceipt.hatTurnPeak > 360, "Its hat spins / " + momentsReceipt.hatTurnPeak.ToString("0"));
            Check(premium == null || premium.DioramaVisitor == null || !premium.DioramaVisitor.DioramaHatSpinning, "The hat settles back on the head");
            Check(w.DioramaSoundCues >= cues + 1, "The birds tweet on the comedy voice");
            // The siphon refusing: a grey puff and a short shake.
            DioramaView(new Vector3(0, .2f, -2.6f), new Vector3(0, 1.1f, -5.2f)); yield return null;
            puffs = w.DioramaGreyPuffs; int shakes = w.DioramaToolShakes;
            Check(w.TryFxCue(FxCueId.HandRefused, ++roomsSequence), "The siphon refuses");
            Check(w.DioramaGreyPuffs == puffs + 1 && w.DioramaToolShakes == shakes + 1 && w.DioramaToolShakeLeft > 0, "A blocked siphon puffs grey and shakes");
            yield return new WaitForSeconds(.08f);
            yield return DioramaShot("moments-siphon-refusal");
            // Fish bubble trails at the display.
            int trails = w.DioramaBubbleTrails;
            DioramaView(CornerEye, CornerTarget);
            yield return WaitFor(() => w.DioramaBubbleTrails > trails, 8, "A display fish lets bubbles go");
            yield return new WaitForSeconds(.2f);
            yield return DioramaShot("moments-bubble-trail");
            momentsReceipt.saleMoments = w.DioramaSaleMoments; momentsReceipt.impactStars = w.DioramaImpactStars; momentsReceipt.greyPuffs = w.DioramaGreyPuffs;
            momentsReceipt.toolShakes = w.DioramaToolShakes; momentsReceipt.bubbleTrails = w.DioramaBubbleTrails; momentsReceipt.soundCues = w.DioramaSoundCues;
            momentsReceipt.flipbookAccents = w.DioramaFlipbookAccents; momentsReceipt.causticSands = w.DioramaCausticSandCount;
            momentsReceipt.voices = shop.GetComponents<AudioSource>().Length;
            Check(momentsReceipt.voices == 5, "The moments' sounds use the existing voices / " + momentsReceipt.voices);
            Check(w.DioramaCausticSandCount >= 3, "The bowls' sand shows moving caustics / " + w.DioramaCausticSandCount);
            shop.Customers.HoldVisitsForValidation = false;
            shop.Customers.SendVisitorsAwayForValidation();
        }

        // ---------------------------------------------------------------- comfort

        IEnumerator RoomsComfort(ShopWorld w)
        {
            // Reduced motion: no shake, no trails, still beads and bowls.
            w.SetFxPreviewModes(true, false); yield return null;
            int shakes = w.DioramaToolShakes, puffs = w.DioramaGreyPuffs;
            DioramaView(new Vector3(0, .2f, -2.6f), new Vector3(0, 1.1f, -5.2f)); yield return null;
            Check(w.TryFxCue(FxCueId.HandRefused, ++roomsSequence), "The siphon refuses in reduced motion");
            Check(w.DioramaGreyPuffs == puffs + 1 && w.DioramaToolShakes == shakes && w.DioramaToolShakeLeft == 0, "Reduced motion keeps the grey puff and drops the shake");
            momentsReceipt.reducedShakes = w.DioramaToolShakes - shakes;
            int trails = w.DioramaBubbleTrails;
            DioramaView(CornerEye, CornerTarget);
            for (float t = 0; t < 6; t += Time.unscaledDeltaTime) yield return null;
            momentsReceipt.reducedTrails = w.DioramaBubbleTrails - trails;
            Check(momentsReceipt.reducedTrails == 0, "Reduced motion lets no bubble trail go");
            yield return DioramaShot("moments-reduced-corner");
            // Low detail: fewer particles in a burst.
            w.SetFxPreviewModes(false, true); yield return null;
            DioramaView(new Vector3(0, .2f, -2.6f), new Vector3(0, 1.1f, -5.2f)); yield return null;
            yield return new WaitForSeconds(1);
            Check(w.TryFxCue(FxCueId.HandRefused, ++roomsSequence), "The siphon refuses in low detail"); yield return null;
            momentsReceipt.lowParticles = w.DioramaLastPuffSize;
            w.SetFxPreviewModes(false, false); yield return new WaitForSeconds(1);
            Check(w.TryFxCue(FxCueId.HandRefused, ++roomsSequence), "The siphon refuses in full detail"); yield return null;
            momentsReceipt.fullParticles = w.DioramaLastPuffSize;
            Check(momentsReceipt.lowParticles < momentsReceipt.fullParticles, "Low detail lets fewer particles go / " + momentsReceipt.lowParticles + " vs " + momentsReceipt.fullParticles);
            // Every moment clears at a cancellation boundary.
            w.CancelFxTransient(); yield return null;
            Check(w.DioramaMomentParticles == 0 && !w.DioramaSaleBag && w.DioramaToolShakeLeft == 0, "A cancellation clears every moment");
        }

        // ---------------------------------------------------------------- timings

        IEnumerator RoomsTimings(ShopWorld w)
        {
            dioramaTiming.device = SystemInfo.deviceModel; dioramaTiming.gpu = SystemInfo.graphicsDeviceName; dioramaTiming.os = SystemInfo.operatingSystem;
            dioramaTiming.quality = QualitySettings.names[QualitySettings.GetQualityLevel()]; dioramaTiming.width = Screen.width; dioramaTiming.height = Screen.height;
            dioramaTiming.targetFrameRate = Application.targetFrameRate; dioramaTiming.vsync = QualitySettings.vSyncCount; dioramaTiming.frameTimingEnabled = FrameTimingManager.IsFeatureEnabled();
            var views = new[] { ("the store and both wings", new Vector3(3.2f, .2f, -.6f), new Vector3(-8f, 1.8f, -3.4f)),
                ("the lab under the culture pipes", new Vector3(-1.5f, .2f, .9f), new Vector3(-1f, 3f, 5f)) };
            foreach (var (view, eye, target) in views)
                foreach (bool look in new[] { true, false, true, false })
                {
                    if (shop.DioramaLookOn != look) shop.SetDioramaLook(look);
                    DioramaView(eye, target);
                    for (int i = 0; i < 90; i++) yield return null;
                    var cpu = new List<double>(); var gpu = new List<double>(); var interval = new List<double>();
                    var timing = new FrameTiming[1];
                    for (int i = 0; i < 240; i++)
                    {
                        FrameTimingManager.CaptureFrameTimings();
                        yield return null;
                        if (FrameTimingManager.GetLatestTimings(1, timing) > 0)
                        {
                            if (timing[0].cpuMainThreadFrameTime > 0) cpu.Add(timing[0].cpuMainThreadFrameTime);
                            if (timing[0].gpuFrameTime > 0) gpu.Add(timing[0].gpuFrameTime);
                        }
                        interval.Add(Time.unscaledDeltaTime * 1000);
                    }
                    double P(List<double> v, double q) { if (v.Count == 0) return 0; v.Sort(); return v[Mathf.Clamp((int)Math.Ceiling(q * v.Count) - 1, 0, v.Count - 1)]; }
                    dioramaTiming.samples.Add(new DioramaTimingSample { view = view, look = look, frames = interval.Count, gpuFrames = gpu.Count,
                        cpuP50 = P(cpu, .5), cpuP95 = P(cpu, .95), gpuP50 = P(gpu, .5), gpuP95 = P(gpu, .95), intervalP50 = P(interval, .5), intervalP95 = P(interval, .95) });
                }
            if (!shop.DioramaLookOn) shop.SetDioramaLook(true);
            Check(dioramaTiming.samples.Count == 8 && dioramaTiming.samples.All(s => s.frames == 240), "Frame timings recorded with the look on and off");
            File.WriteAllText(Path.Combine(evidence, "diorama-timing.json"), JsonUtility.ToJson(dioramaTiming, true));
            File.WriteAllText(Path.Combine(evidence, "moments-trace.json"), JsonUtility.ToJson(momentsReceipt, true));
        }
    }
}
#endif
