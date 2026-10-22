#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    // AD-C characters (-evolutionDioramaCharactersSmoke): the four customer types and the three crew on the one humanoid rig.
    // Every customer type arrives, browses and reacts (haggling, pleased, shocked, slapped) in the shop; the crew are hired and
    // do real work (Nori carries stock in the crate, Pip checks a customer out from the step stool behind the counter, Moss
    // doses a recipe); the crew rebuild in place when the look switches; the comfort modes; the character studies' types,
    // crew and reactions through their controls; and frame timings with the crew in view.
    public sealed partial class MilestoneSmoke
    {
        [Serializable] sealed class CharacterReceipt
        {
            public string role, kit;
            public bool crew;
            public float floorGap, headTop, haggleMouth, haggleLids, haggleBrow, happyMouth, shockedMouth, ouchMouth, ouchLids, starHeight;
            public float faceOverCounter, handsOverCounter;
            public List<string> moodsSeen = new List<string>();
            public List<string> materials = new List<string>();
        }
        [Serializable] sealed class CharacterTrace { public List<CharacterReceipt> roles = new List<CharacterReceipt>(); public List<string> notes = new List<string>(); }
        readonly CharacterTrace characterTrace = new CharacterTrace();
        readonly Dictionary<string, CharacterReceipt> characterReceipts = new Dictionary<string, CharacterReceipt>();

        /// <summary>The authored countertop height (the FX15 route holds Pip's hands above it).</summary>
        const float CountertopHeight = 1.145f;

        CharacterReceipt CharacterReceiptOf(string role)
        {
            if (!characterReceipts.TryGetValue(role, out var receipt))
            {
                DioramaRoles.TryGet(role, out var spec);
                receipt = new CharacterReceipt { role = role, kit = spec != null ? spec.Kit : null, crew = spec != null && spec.Crew };
                characterReceipts[role] = receipt; characterTrace.roles.Add(receipt);
            }
            return receipt;
        }

        IEnumerator DioramaCharactersScenario()
        {
            limitations.Add("AD-C characters: isolated slot; scripted steps choose each visitor's type and start arrivals with ShopCustomers.SpawnNow (validation hook), and the customers' pleased, shocked and slapped reactions are started with the presentation calls the game itself uses (their walking and browsing are real frames) while the visit is held at the display, the visitor turned to face the camera for the close-ups. The crew are hired with labelled 300-credit funds; after its delivery the held stocker is moved back to its starting place, out of the visitors' way (an idle stocker waits where it finished); the stocker's source specimen, the display stock and the technician's ingredients use accelerated model ticks; their work (carrying, checking out, dosing) is real frames. A crew member being caught out is started with the worker event hook the game's refusal path uses (the refusal itself is FX15's). Scripted camera views; the character studies' controls pressed with the synthetic Input System mouse. Screens are not hands-on review, creative approval or a minimum-spec GPU result.");
            Check(shop.ValidationMode && Path.GetFullPath(shop.SavePath) == Path.GetFullPath(Path.Combine(evidence, "smoke-save.json")), "AD-C isolated save only");
            yield return new WaitForSeconds(1);
            previousInputSettings = InputSystem.settings; smokeInputSettings = Instantiate(previousInputSettings);
            smokeInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = smokeInputSettings;
            syntheticKeyboard = InputSystem.AddDevice<Keyboard>("AD-C keyboard"); cycleMouse = InputSystem.AddDevice<Mouse>("AD-C mouse"); cardMouse = cycleMouse;
            syntheticKeyboard.MakeCurrent(); cycleMouse.MakeCurrent();
            if (!Application.isEditor)
            {
                int width = int.Parse(ShopSession.Arg("-screen-width")), height = int.Parse(ShopSession.Arg("-screen-height"));
                Check(Screen.width == width && Screen.height == height, "Actual requested Windows resolution " + width + "x" + height);
            }
            GateFresh();
            var w = shop.World;
            yield return CharactersFoundation(w);
            yield return CrewHired(w);
            yield return CrewStocker(w);
            yield return CrewTechnician(w);
            // Display stock for the visitors to browse (tank-a has Nori's delivery).
            SeedStock("bubblebelly", "tank-b"); shop.World.Refresh(shop.Model);
            yield return EnterCounter();
            Check(shop.OpenForDay(), "Open the shop for the visitors");
            shop.SetPanel(ShopPanel.None);
            foreach (var type in DioramaRoles.Customers) yield return CharacterCustomer(w, type);
            yield return CrewCashier(w);
            yield return CrewCaughtOut(w);
            yield return CharacterToggle(w);
            yield return CharacterComfort(w);
            yield return CharacterStudy(w);
            yield return CharacterTimings(w);
            foreach (var role in DioramaRoles.CrewRoles)
            {
                var moods = CharacterReceiptOf(role).moodsSeen;
                Check(moods.Count >= 3, "The crew member shows its reactions at work / " + role + " / " + string.Join(", ", moods));
            }
            File.WriteAllText(Path.Combine(evidence, "character-trace.json"), JsonUtility.ToJson(characterTrace, true));
            shop.SetPanel(ShopPanel.Pause);
        }

        // ---------------------------------------------------------------- foundation

        IEnumerator CharactersFoundation(ShopWorld w)
        {
            Check(w.DioramaActive && DioramaLook.Active && shop.DioramaLookOn, "The characters route switches the diorama look on");
            Check(DioramaRoles.All.Count == 7 && DioramaRoles.Customers.Count == 4 && DioramaRoles.CrewRoles.Count == 3, "Four customer types and three crew on one rig");
            foreach (var role in DioramaRoles.All)
            {
                var kit = Resources.Load<GameObject>(DioramaLook.KitPath + role.Kit);
                Check(kit, "Kit loads / " + role.Kit);
                if (!kit) continue;
                foreach (var part in new[] { "Left hip", "Right hip", "Body", "Left shoulder", "Right shoulder", "Head pivot" })
                    Check(kit.transform.Find(part), "The humanoid rig's joint / " + role.Id + " / " + part);
                foreach (var part in ShopCharacterVisual.DioramaHeadParts)
                    Check(kit.transform.Find(part), "The humanoid rig's face part / " + role.Id + " / " + part);
                var mouth = kit.transform.Find("Mouth") ? kit.transform.Find("Mouth").GetComponent<SkinnedMeshRenderer>() : null;
                var lids = kit.transform.Find("Lids") ? kit.transform.Find("Lids").GetComponent<SkinnedMeshRenderer>() : null;
                foreach (var key in new[] { "Happy", "Haggle", "Shocked", "Ouch" })
                    Check(mouth && mouth.sharedMesh && mouth.sharedMesh.GetBlendShapeIndex(key) >= 0, "Mouth shape key / " + role.Id + " / " + key);
                Check(lids && lids.sharedMesh && lids.sharedMesh.GetBlendShapeIndex("Blink") >= 0, "Lids blink / " + role.Id);
                Check((kit.transform.Find("Carry crate") != null) == role.Crew, "Only the crew carry a crate / " + role.Id);
                Check((kit.transform.Find("Step stool") != null) == (role.Id == "cashier"), "Only Pip stands on a step stool / " + role.Id);
                var materials = new HashSet<string>();
                foreach (var r in kit.GetComponentsInChildren<Renderer>(true)) foreach (var m in r.sharedMaterials) if (m) materials.Add(m.name.Replace(" (Instance)", ""));
                Check(role.Crew ? materials.Contains("DK_CrewTeal") : !materials.Any(m => m.StartsWith("DK_Crew")),
                    role.Crew ? "Crew wear shop teal / " + role.Id : "Customers never wear crew teal / " + role.Id);
                foreach (var m in materials) Check(DioramaLook.TryGetSlotColor(m, out _), "Every kit slot has its palette colour / " + role.Id + " / " + m);
                CharacterReceiptOf(role.Id).materials = materials.OrderBy(m => m).ToList();
            }
            yield return null;
        }

        /// <summary>The world bounds of a figure's own meshes (not its owner's signs, captions or slap stars).</summary>
        static Bounds FigureBounds(Transform figure)
        {
            bool any = false; var bounds = new Bounds();
            foreach (var r in figure.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy || r is ParticleSystemRenderer) continue;
                bool label = false;
                for (var t = r.transform; t && t != figure; t = t.parent)
                {
                    string n = t.name.ToLowerInvariant();
                    if (n.Contains("sign") || n.Contains("caption") || n.Contains("star") || n.Contains("name") || n.Contains("parcel")) label = true;
                }
                if (label) continue;
                if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
            }
            return bounds;
        }

        static float LowestVertex(Transform part)
        {
            float low = float.MaxValue;
            if (!part) return low;
            foreach (var filter in part.GetComponentsInChildren<MeshFilter>())
                if (filter.sharedMesh && filter.gameObject.activeInHierarchy)
                    foreach (var v in filter.sharedMesh.vertices) low = Mathf.Min(low, filter.transform.TransformPoint(v).y);
            return low;
        }

        /// <summary>The lowest point of a kit arm's hand: its vertices farthest from the shoulder (the sleeve by the shoulder sits
        /// over the body, behind the counter).</summary>
        static float LowestHand(Transform arm)
        {
            var filter = arm ? arm.GetComponent<MeshFilter>() : null;
            if (!filter || !filter.sharedMesh) return float.MaxValue;
            var vertices = filter.sharedMesh.vertices;
            float reach = 0; foreach (var v in vertices) reach = Mathf.Max(reach, v.magnitude);
            float low = float.MaxValue;
            foreach (var v in vertices) if (v.magnitude >= reach * .8f) low = Mathf.Min(low, filter.transform.TransformPoint(v).y);
            return low;
        }

        static bool WearsCrewTeal(ShopCharacterVisual figure) =>
            figure.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Any(m => m && m.name.Contains("DK_CrewTeal"));
        static bool WearsAnyCrewSlot(Transform figure) =>
            figure.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Any(m => m && m.name.Contains("DK_Crew"));

        Transform HeadOf(ShopCharacterVisual figure) => figure ? figure.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Head pivot") : null;

        /// <summary>A close view of a figure's face: the first spot (in front, then to either side) from which the line to the face
        /// passes no collider but the figure's own, so the camera is never behind glass, a wall or a station.</summary>
        void FaceView(ShopCharacterVisual figure, float distance = 1.25f, float aside = .25f)
        {
            var head = HeadOf(figure);
            var face = head ? head.position + Vector3.up * .33f : figure.transform.position + Vector3.up * 1.3f;
            var owner = figure.transform.parent && figure.transform.parent.GetComponent<CustomerActor>() ? figure.transform.parent : figure.transform;
            var spots = new[] { new Vector3(aside, 0, distance), new Vector3(1.0f, 0, 1.0f), new Vector3(-1.0f, 0, 1.0f), new Vector3(1.4f, 0, .45f),
                new Vector3(-1.4f, 0, .45f), new Vector3(1.5f, 0, -.2f), new Vector3(-1.5f, 0, -.2f) };
            Check(ClearView(owner, face, face, spots.Select(o => figure.transform.TransformPoint(o))), "A clear view of the face / " + figure.name);
        }

        /// <summary>Stands the view at the first spot from which the camera is inside no collider and its line to the subject
        /// passes none but the owner's own, aimed at the given point.</summary>
        bool ClearView(Transform owner, Vector3 subject, Vector3 aim, IEnumerable<Vector3> spots)
        {
            bool Other(Collider c) => !c.transform.IsChildOf(owner) && !c.transform.IsChildOf(shop.Player.transform);
            foreach (var at in spots)
            {
                var spot = at; spot.y = .2f;
                DioramaView(spot, aim);
                var eye = shop.Player.View.transform.position;
                bool inside = Physics.OverlapSphere(eye, .12f).Any(Other);
                bool blocked = Physics.RaycastAll(eye, (subject - eye).normalized, Vector3.Distance(eye, subject) - .05f).Any(h => Other(h.collider));
                if (!inside && !blocked) return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- customers

        IEnumerator CharacterCustomer(ShopWorld w, string type)
        {
            DioramaRoles.TryGet(type, out var role);
            var receipt = CharacterReceiptOf(type);
            var visit = Spawn(type);
            var actor = shop.Customers.ActorOf(visit);
            var figure = actor ? actor.DioramaVisitor : null;
            Check(figure && figure.IsDiorama && figure.DioramaRole == role, "The " + type + " arrives as its kit figure");
            if (!figure) yield break;
            var parts = figure.GetComponentsInChildren<Transform>(true);
            foreach (var joint in new[] { "Left hip", "Right hip", "Left shoulder", "Right shoulder", "Head pivot" })
                Check(parts.Any(t => t.name == joint), "Classic joint names keep its walk and reactions / " + type + " / " + joint);
            var head = HeadOf(figure);
            foreach (var part in ShopCharacterVisual.DioramaHeadParts) Check(head && head.Find(part), "The face rides on the head pivot / " + type + " / " + part);
            Check(!WearsAnyCrewSlot(figure.transform), "A customer never wears the crew's teal / " + type);
            float start = Time.realtimeSinceStartup;
            float walkLow = float.MaxValue, walkHigh = float.MinValue;
            while (actor && actor.Phase != CustomerPhase.Browsing && Time.realtimeSinceStartup - start < 25)
            {
                var b = FigureBounds(figure.transform);
                walkLow = Mathf.Min(walkLow, b.min.y - actor.transform.position.y); walkHigh = Mathf.Max(walkHigh, b.min.y - actor.transform.position.y);
                yield return null;
            }
            Check(actor && actor.Phase == CustomerPhase.Browsing, "The " + type + " walks to a display and browses");
            Check(walkLow > -.06f && walkLow < .06f, "Its feet stay on the floor while it walks / " + type + " / lowest " + walkLow.ToString("0.000") + ", highest " + walkHigh.ToString("0.000"));
            yield return new WaitForSeconds(.7f);
            var bounds = FigureBounds(figure.transform);
            receipt.floorGap = bounds.min.y - actor.transform.position.y;
            receipt.headTop = bounds.max.y - actor.transform.position.y;
            Check(Mathf.Abs(receipt.floorGap) < .03f, "Standing on the floor while browsing / " + type + " / " + receipt.floorGap.ToString("0.000"));
            Check(Mathf.Abs(receipt.headTop - role.HeadTop) < .08f, "Its height is the kit's / " + type + " / " + receipt.headTop.ToString("0.00") + " vs " + role.HeadTop);
            receipt.haggleMouth = figure.DioramaMouthWeight(DioramaVisitorMood.Haggle);
            receipt.haggleLids = figure.DioramaLidWeight; receipt.haggleBrow = figure.DioramaBrowLift;
            Check(actor.DioramaMood == DioramaVisitorMood.Haggle && receipt.haggleMouth > .7f, "Its face studies the price while browsing / " + type);
            if (role.Squint > 0) Check(receipt.haggleLids >= role.Squint * .9f - .01f, "It squints its own way while haggling / " + type + " / " + receipt.haggleLids.ToString("0.00"));
            Check(Mathf.Abs(receipt.haggleBrow - .85f * role.BrowTilt) < 2f, "It lifts one brow its own way while haggling / " + type + " / " + receipt.haggleBrow.ToString("0.0"));
            receipt.moodsSeen.Add("Haggle");
            // Hold the visit at the display while each reaction is shown and read (it would otherwise decide and move on).
            shop.Customers.HoldVisitsForValidation = true;
            Check(shop.Customers.HoldVisitsForValidation, "Hold the visit for the reactions (validation only)");
            Check(actor.Phase == CustomerPhase.Browsing, "The visit holds at the display / " + type);
            // From behind and beside it (it faces the display it studies), so the shot shows both.
            Vector3 headPoint = actor.transform.position + Vector3.up * (role.HeadTop - .4f);
            var behind = new[] { new Vector3(1.3f, 0, -2.4f), new Vector3(-1.3f, 0, -2.4f), new Vector3(0, 0, -2.4f), new Vector3(1.1f, 0, -1.8f), new Vector3(-1.1f, 0, -1.8f) }
                .Select(o => actor.transform.TransformPoint(o));
            Check(ClearView(actor.transform, headPoint, headPoint - Vector3.up * .3f + actor.transform.forward * .6f, behind), "A clear view of the " + type + " at the display");
            yield return DioramaShot("character-" + type + "-browse");
            // The close-ups: the held visitor turns from the display to face the open floor (presentation only; a browsing
            // visitor stands still, so nothing turns it back meanwhile), where the camera stands clear of the display.
            var away = -actor.transform.forward; away.y = 0;
            actor.transform.rotation = Quaternion.LookRotation(away.normalized) * Quaternion.Euler(0, -15, 0);
            yield return null;
            FaceView(figure, 1.3f, .2f);
            yield return DioramaShot("character-" + type + "-haggle");
            yield return CustomerReacts(actor, figure, type, FxCustomerReaction.Accepted, DioramaVisitorMood.Happy, r => r.happyMouth = figure.DioramaMouthWeight(DioramaVisitorMood.Happy));
            yield return CustomerReacts(actor, figure, type, FxCustomerReaction.Refused, DioramaVisitorMood.Shocked, r => r.shockedMouth = figure.DioramaMouthWeight(DioramaVisitorMood.Shocked));
            Check(actor.PlaySlapReaction(), "The " + type + " can be slapped");
            yield return new WaitForSeconds(.35f);
            receipt.ouchMouth = figure.DioramaMouthWeight(DioramaVisitorMood.Ouch); receipt.ouchLids = figure.DioramaLidWeight;
            Check(actor.DioramaMood == DioramaVisitorMood.Ouch && receipt.ouchMouth > .8f && receipt.ouchLids > .6f, "Slapped: it winces / " + type);
            var stars = actor.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Cartoon slap stars");
            if (stars) receipt.starHeight = stars.localPosition.y;
            Check(stars && stars.gameObject.activeInHierarchy && Mathf.Abs(receipt.starHeight - role.StarHeight) < .01f, "The slap stars circle above its own hat / " + type + " / " + receipt.starHeight);
            receipt.moodsSeen.Add("Ouch");
            yield return DioramaShot("character-" + type + "-ouch");
            shop.Customers.SendVisitorsAwayForValidation();
            shop.Customers.HoldVisitsForValidation = false;
            yield return null; yield return null;
        }

        IEnumerator CustomerReacts(CustomerActor actor, ShopCharacterVisual figure, string type, FxCustomerReaction reaction, DioramaVisitorMood mood, Action<CharacterReceipt> record)
        {
            Check(actor.PlayCustomerReaction(reaction), "The " + type + " reacts / " + reaction);
            yield return new WaitForSeconds(.3f);
            Check(actor.DioramaMood == mood && figure.DioramaMouthWeight(mood) > .8f, "Its face shows it / " + type + " / " + mood + " " + figure.DioramaMouthWeight(mood).ToString("0.00"));
            var receipt = CharacterReceiptOf(type); record(receipt); receipt.moodsSeen.Add(mood.ToString());
            yield return DioramaShot("character-" + type + "-" + mood.ToString().ToLowerInvariant());
        }

        // ---------------------------------------------------------------- crew

        ShopCharacterVisual CrewActor(string role) =>
            role == "cashier" ? shop.Cashier.Actor : role == "stocker" ? shop.Stocker.Actor : role == "technician" ? shop.Technician.Actor : null;

        /// <summary>Records each crew member's reaction every frame, and holds Pip's face and hands above the countertop.</summary>
        void CrewObserve()
        {
            foreach (var role in DioramaRoles.CrewRoles)
            {
                var actor = CrewActor(role);
                if (!actor || !actor.isActiveAndEnabled || !actor.IsDiorama) continue;
                var receipt = CharacterReceiptOf(role);
                string mood = actor.CrewMood.ToString();
                if (actor.CrewMood != DioramaVisitorMood.Content && !receipt.moodsSeen.Contains(mood)) receipt.moodsSeen.Add(mood);
                if (role == "cashier" && actor.WorkerSnapshot.Activity == FxWorkerActivity.Checkout && actor.WorkerSnapshot.EventKind == FxWorkerEvent.None && !shop.Paused)
                {
                    float hands = Mathf.Min(LowestHand(actor.LeftArm), LowestHand(actor.RightArm));
                    receipt.handsOverCounter = receipt.handsOverCounter == 0 ? hands - CountertopHeight : Mathf.Min(receipt.handsOverCounter, hands - CountertopHeight);
                    if (hands < CountertopHeight) throw new InvalidOperationException("Pip's hands sink below the countertop while checking out / " + hands);
                }
            }
        }

        IEnumerator CrewWait(Func<bool> ready, float timeout, string reason)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!ready())
            {
                CrewObserve();
                if (Time.realtimeSinceStartup > end)
                    throw new TimeoutException("AD-C " + reason + " / " + shop.Cashier.Status + " / " + shop.Stocker.Status + " / " + shop.Technician.Status);
                yield return null;
            }
            CrewObserve();
            Check(true, "Actual crew transition / " + reason);
        }

        IEnumerator CrewFor(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) { CrewObserve(); yield return null; }
        }

        IEnumerator CrewHired(ShopWorld w)
        {
            StaffCreditFixture(300, "AD-C isolated hiring fixture");
            yield return StaffEnter(); Check(shop.HireCashier() && shop.SetCashierScheduled(true) && shop.SetCashierPaused(true), "Hire and pay Pip, hold work");
            yield return StockerEnter(); Check(shop.HireStocker() && shop.SetStockerScheduled(true) && shop.SetStockerPaused(true), "Hire and pay Nori, hold work");
            yield return TechnicianEnter(); Check(shop.HireTechnician() && shop.SetTechnicianScheduled(true) && shop.SetTechnicianPaused(true), "Hire and pay Moss, hold work");
            shop.SetPanel(ShopPanel.None);
            yield return null; yield return null;
            foreach (var roleId in DioramaRoles.CrewRoles)
            {
                DioramaRoles.TryGet(roleId, out var role);
                var actor = CrewActor(roleId);
                Check(actor && actor.gameObject.activeInHierarchy && actor.IsDiorama && actor.DioramaRole == role, "The hired " + roleId + " is its kit figure");
                if (!actor) continue;
                Check(WearsCrewTeal(actor), "The crew wear shop teal / " + roleId);
                Check(actor.WorkerSnapshot.WorkerId == ShopStaff.CashierId || actor.WorkerSnapshot.WorkerId == ShopStaff.StockerId || actor.WorkerSnapshot.WorkerId == ShopStaff.TechnicianId,
                    "The kit figure keeps its worker binding / " + roleId);
                Check(actor.WorkerProxy && actor.WorkerProxy.Find("Carry crate") && !actor.WorkerProxy.gameObject.activeSelf, "The crate hangs on the carry socket, hidden until carrying / " + roleId);
                var b = FigureBounds(actor.transform);
                var receipt = CharacterReceiptOf(roleId);
                receipt.floorGap = b.min.y - actor.transform.position.y; receipt.headTop = b.max.y - actor.transform.position.y;
                Check(Mathf.Abs(receipt.floorGap) < .03f, "Standing on the floor / " + roleId + " / " + receipt.floorGap.ToString("0.000"));
                Check(Mathf.Abs(receipt.headTop - role.HeadTop) < .08f, "Its height is the kit's / " + roleId + " / " + receipt.headTop.ToString("0.00") + " vs " + role.HeadTop);
            }
            // Pip on the step stool: the face clears the countertop, seen from the front of the queue.
            var pip = shop.Cashier.Actor; var pipHead = HeadOf(pip);
            var eyes = pipHead ? pipHead.Find("Eyes") : null; var mouth = pipHead ? pipHead.Find("Mouth") : null;
            Check(eyes && mouth, "Pip's face found");
            if (eyes && mouth)
            {
                float lowest = Mathf.Min(eyes.GetComponent<Renderer>().bounds.min.y, mouth.GetComponent<Renderer>().bounds.min.y);
                var pipReceipt = CharacterReceiptOf("cashier"); pipReceipt.faceOverCounter = lowest - CountertopHeight;
                Check(pipReceipt.faceOverCounter > .1f, "Pip's eyes and mouth clear the countertop / " + pipReceipt.faceOverCounter.ToString("0.00"));
                // A Casual visitor's eyes (the shortest customer) at the front of the queue; the line to Pip's face must miss every
                // visible part of the counter (its tablet, bell and art included). The counter's own collider is a tall
                // invisible interaction volume, so this reads the drawn meshes' bounds instead.
                var nav = shop.Customers.Navigation;
                var visitorEye = nav.QueueSpot(0) + Vector3.up * 1.3f;
                var counterArt = StationOf(StationKind.Counter).GetComponentsInChildren<Renderer>()
                    .Where(r => r.enabled && r.gameObject.activeInHierarchy && !r.transform.IsChildOf(pip.transform)).ToList();
                foreach (var target in new[] { eyes, mouth })
                {
                    var point = target.GetComponent<Renderer>().bounds.center;
                    var ray = new Ray(visitorEye, (point - visitorEye).normalized);
                    float distance = Vector3.Distance(visitorEye, point) - .02f;
                    var blocking = counterArt.Where(r => r.bounds.IntersectRay(ray, out float d) && d < distance).Select(r => r.name).ToList();
                    Check(blocking.Count == 0, "A visitor at the counter sees Pip's " + target.name.ToLowerInvariant() + " over it / " + string.Join(", ", blocking));
                }
            }
            Check(SignClearsHead(pip), "Pip's duty sign rides above the visor / " + SignGap(pip).ToString("0.00"));
            var counter = StationOf(StationKind.Counter).transform;
            DioramaView(shop.Customers.Navigation.QueueSpot(0) + counter.forward * -.4f, pip.transform.position + Vector3.up * 1.45f);
            yield return new WaitForSeconds(.5f);
            yield return DioramaShot("crew-pip-counter");
            // From beside the counter, so the step stool shows: the first spot with a clear view of Pip.
            var pipMiddle = pip.transform.position + Vector3.up * 1.0f;
            var spots = new[] { new Vector3(-2.6f, .2f, .97f), new Vector3(2.6f, .2f, .97f), new Vector3(-2.3f, .2f, -1.5f), new Vector3(2.3f, .2f, -1.5f) }
                .Select(p => counter.TransformPoint(p)).ToList();
            var clear = spots.FirstOrDefault(p => !Physics.RaycastAll(p + Vector3.up * 1.4f, (pipMiddle - (p + Vector3.up * 1.4f)).normalized, Vector3.Distance(pipMiddle, p + Vector3.up * 1.4f))
                .Any(h => !h.transform.IsChildOf(counter) && !h.transform.IsChildOf(pip.transform) && !h.transform.IsChildOf(shop.Player.transform)));
            Check(clear != default, "A clear view of Pip from beside the counter");
            DioramaView(clear, pipMiddle);
            yield return DioramaShot("crew-pip-stool");
        }

        float SignGap(ShopCharacterVisual pip)
        {
            var sign = pip.transform.Find("Cashier duty sign");
            var backing = sign ? sign.GetComponentsInChildren<Renderer>().FirstOrDefault(r => r.name == "Duty sign backing") : null;
            return backing ? backing.bounds.min.y - FigureBounds(pip.transform).max.y : -1;
        }
        bool SignClearsHead(ShopCharacterVisual pip) => SignGap(pip) > .03f;

        IEnumerator CrewStocker(ShopWorld w)
        {
            yield return EditedPlace(null, ShopLayout.RackDefinitionId, 9, 7, 0, new Vector3(4.5f, .2f, 1.3f));
            string rack = StockerRack(); var slots = shop.Layout.GetStorageSlotIds(rack);
            SpecimenProduce("pondlet");
            yield return SpecimenRackPanel(rack);
            Check(shop.StoreReadyInRack(slots[0]), "A labelled pondlet in the rack for Nori");
            yield return StockerEnter();
            Check(shop.SetStockingTarget("tank-a", "pondlet") && shop.SetStockerPaused(false), "Nori starts a real delivery");
            StockerParkPlayer();
            var nori = shop.Stocker.Actor;
            yield return CrewWait(() => shop.Stocker.Carrying, 35, "Nori carries the pondlet");
            yield return CrewFor(.4f);
            var crate = nori.WorkerProxy ? nori.WorkerProxy.Find("Carry crate") : null;
            Check(crate && crate.gameObject.activeInHierarchy, "Nori carries the crate while carrying stock");
            if (crate)
            {
                var local = nori.transform.InverseTransformPoint(crate.GetComponentInChildren<Renderer>().bounds.center);
                Check(Mathf.Abs(local.x) < .12f && local.z > .15f && local.z < .75f && local.y > .5f && local.y < 1.35f, "The crate is held in front of the body / " + local);
            }
            WorkerAim(nori, true);
            yield return DioramaShot("crew-nori-carry");
            yield return CrewWait(() => shop.Stocker.WorkerActivity == FxWorkerActivity.Place, 35, "Nori places it in the display");
            WorkerAim(nori);
            yield return CrewFor(.25f);
            Check(nori.CrewMood == DioramaVisitorMood.Focus, "Nori concentrates while placing / " + nori.CrewMood);
            yield return DioramaShot("crew-nori-place");
            int jobs = shop.Stocker.CompletedJobs;
            yield return CrewWait(() => shop.Stocker.CompletedJobs > jobs, 15, "Nori finishes the delivery");
            yield return CrewFor(.6f);
            Check(CharacterReceiptOf("stocker").moodsSeen.Contains("Happy"), "Nori is pleased with the delivery");
            Check(shop.Model.GetTank("tank-a").HasValue, "The display holds Nori's pondlet");
            yield return StockerEnter(); Check(shop.SetStockerPaused(true), "Hold Nori");
            shop.SetPanel(ShopPanel.None);
            // An idle stocker waits where it finished, at the display the visitors browse next; the pictures need it out of
            // their way, so it waits at the back of the lab, Moss's starting place (presentation only, while held), clear of the
            // views of Moss at the incubator.
            nori.transform.SetPositionAndRotation(new Vector3(0, 0, 4.4f), Quaternion.Euler(0, 180, 0));
            Physics.SyncTransforms();
            yield return null;
        }

        IEnumerator CrewTechnician(ShopWorld w)
        {
            shop.Model.Tick(60);
            yield return TechnicianEnter();
            TechnicianCollect("pondlet");
            Check(shop.QueueTechnicianRecipe("pondlet") && shop.SetTechnicianPaused(false), "Moss starts a real recipe");
            StockerParkPlayer();
            var moss = shop.Technician.Actor;
            yield return CrewWait(() => shop.Technician.WorkerActivity == FxWorkerActivity.Measure || shop.Technician.WorkerActivity == FxWorkerActivity.Dose, 25, "Moss measures and doses");
            WorkerAim(moss, true);
            yield return CrewFor(.25f);
            Check(CharacterReceiptOf("technician").moodsSeen.Contains("Focus"), "Moss concentrates while measuring and dosing / " + moss.CrewMood);
            yield return DioramaShot("crew-moss-dose");
            int jobs = shop.Technician.CompletedJobs;
            yield return CrewWait(() => shop.Technician.Carrying, 40, "Moss carries the hatched creature");
            yield return CrewFor(.3f);
            WorkerAim(moss, true);
            yield return DioramaShot("crew-moss-carry");
            yield return CrewWait(() => shop.Technician.CompletedJobs > jobs, 40, "Moss stores it in the rack");
            yield return CrewFor(.6f);
            Check(CharacterReceiptOf("technician").moodsSeen.Contains("Happy"), "Moss is pleased with the stored creature");
            yield return TechnicianEnter(); Check(shop.SetTechnicianPaused(true), "Hold Moss");
            shop.SetPanel(ShopPanel.None);
        }

        CustomerVisit queuedVisit;
        /// <summary>Sends Casual visitors until one queues at the counter (one may turn a price down and leave).</summary>
        IEnumerator VisitorQueues(string reason)
        {
            queuedVisit = null;
            for (int attempt = 1; attempt <= 3 && queuedVisit == null; attempt++)
            {
                var visit = Spawn(CustomerArchetypes.Casual);
                yield return CrewWait(() => visit.Phase == CustomerPhase.Queued || visit.Phase == CustomerPhase.Leaving || visit.Phase == CustomerPhase.Finished, 45, reason + " / attempt " + attempt);
                if (visit.Phase == CustomerPhase.Queued) queuedVisit = visit;
                else yield return CrewWait(() => shop.Customers.ActiveCount == 0, 30, "the visitor who turned the price down leaves");
            }
            Check(queuedVisit != null, "A visitor queues at the counter / " + reason);
        }

        IEnumerator CrewCashier(ShopWorld w)
        {
            yield return VisitorQueues("for Pip");
            var visit = queuedVisit;
            yield return StaffEnter(); Check(shop.SetCashierPaused(false), "Pip serves");
            var pip = shop.Cashier.Actor;
            var counter = StationOf(StationKind.Counter).transform;
            DioramaView(counter.TransformPoint(new Vector3(1.9f, .2f, -1.7f)), pip.transform.position + Vector3.up * 1.4f);
            yield return CrewFor(.4f); // the staff panel fades out
            yield return CrewWait(() => pip.CrewMood == DioramaVisitorMood.Greet, 8, "Pip greets the customer");
            yield return DioramaShot("crew-pip-greet");
            yield return CrewWait(() => pip.CrewMood == DioramaVisitorMood.Focus, 8, "Pip rings the sale up");
            yield return DioramaShot("crew-pip-focus");
            yield return CrewWait(() => pip.CrewMood == DioramaVisitorMood.Happy, 8, "Pip is pleased with the sale");
            yield return DioramaShot("crew-pip-happy");
            Check(visit.Outcome == CustomerOutcome.Sold, "Pip's checkout is a real sale");
            Check(CharacterReceiptOf("cashier").handsOverCounter > 0, "Pip's hands stay over the countertop while checking out / " + CharacterReceiptOf("cashier").handsOverCounter.ToString("0.00"));
            yield return CrewWait(() => shop.Customers.ActiveCount == 0, 20, "the customer leaves");
            yield return StaffEnter(); Check(shop.SetCashierPaused(true), "Hold Pip");
            shop.SetPanel(ShopPanel.None);
        }

        IEnumerator CrewCaughtOut(ShopWorld w)
        {
            yield return CrewFor(.5f); // the staff panel fades out
            foreach (var role in DioramaRoles.CrewRoles)
            {
                var actor = CrewActor(role);
                Check(actor.PlayWorkerEvent(FxWorkerEvent.Refused, "adc-caught-out-" + role), "The " + role + " is caught out");
                // Pip's face from the front of the queue (the counter is in front of Pip); the others close up.
                if (role == "cashier")
                {
                    var counter = StationOf(StationKind.Counter).transform;
                    DioramaView(shop.Customers.Navigation.QueueSpot(0) + counter.forward * -.4f, actor.transform.position + Vector3.up * 1.6f);
                }
                else FaceView(actor, 1.15f, .2f);
                yield return null;
                CrewObserve();
                Check(actor.CrewMood == DioramaVisitorMood.Shocked, "Caught out: its face shows it / " + role);
                yield return DioramaShot("crew-" + role + "-caught-out");
                yield return CrewWait(() => actor.CrewMood != DioramaVisitorMood.Shocked, 5, role + " recovers");
            }
        }

        // ---------------------------------------------------------------- the look switched with the crew in the shop

        IEnumerator CharacterToggle(ShopWorld w)
        {
            var before = new Dictionary<string, (Vector3 position, Quaternion rotation, string labels, string worker)>();
            string LabelsOf(ShopCharacterVisual a) => string.Join(",", a.transform.Cast<Transform>().Select(t => t.name).Where(n => n.ToLowerInvariant().Contains("sign") || n.ToLowerInvariant().Contains("name")).OrderBy(n => n));
            foreach (var role in DioramaRoles.CrewRoles)
            {
                var a = CrewActor(role);
                before[role] = (a.transform.position, a.transform.rotation, LabelsOf(a), a.WorkerSnapshot.WorkerId);
            }
            int materialsOn = -1;
            for (int cycle = 1; cycle <= 3; cycle++)
            {
                shop.SetDioramaLook(false); yield return null;
                foreach (var role in DioramaRoles.CrewRoles)
                {
                    var a = CrewActor(role);
                    Check(a && !a.IsDiorama && a.gameObject.activeInHierarchy, "Look off: the " + role + " is the classic figure again / " + cycle);
                    Check(a.transform.position == before[role].position && a.transform.rotation == before[role].rotation && LabelsOf(a) == before[role].labels &&
                        a.WorkerSnapshot.WorkerId == before[role].worker, "Rebuilt in place, keeping its sign and worker binding / " + role + " / " + cycle);
                    Check(!a.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Carry crate" || t.name == "Step stool"), "No kit part left behind / " + role + " / " + cycle);
                }
                Check(w.DioramaMaterialsConsistent(out var off), "Every swapped renderer is back to its classic material / " + cycle + " " + off);
                Check(SignClearsHead(shop.Cashier.Actor), "The duty sign rides above the classic cashier's cap / " + cycle + " / " + SignGap(shop.Cashier.Actor).ToString("0.00"));
                if (cycle == 1)
                {
                    var counter = StationOf(StationKind.Counter).transform;
                    DioramaView(shop.Customers.Navigation.QueueSpot(0) + counter.forward * -.4f, shop.Cashier.Actor.transform.position + Vector3.up * 1.45f);
                    yield return DioramaShot("crew-classic-counter");
                }
                shop.SetDioramaLook(true); yield return null;
                foreach (var role in DioramaRoles.CrewRoles)
                {
                    var a = CrewActor(role);
                    Check(a && a.IsDiorama && a.DioramaRole.Id == role && WearsCrewTeal(a), "Look on again: the kit " + role + " is back / " + cycle);
                    Check(a.transform.position == before[role].position && LabelsOf(a) == before[role].labels && a.WorkerSnapshot.WorkerId == before[role].worker,
                        "Rebuilt in place again / " + role + " / " + cycle);
                    Check(a.WorkerProxy && a.WorkerProxy.Find("Carry crate") && !a.WorkerProxy.gameObject.activeSelf, "The crate is back on its socket, hidden / " + role + " / " + cycle);
                }
                Check(SignClearsHead(shop.Cashier.Actor), "Pip's duty sign rides above the visor again / " + cycle + " / " + SignGap(shop.Cashier.Actor).ToString("0.00"));
                yield return null;
                int materials = Resources.FindObjectsOfTypeAll<Material>().Length;
                if (cycle == 1) materialsOn = materials;
                else Check(materials <= materialsOn, "Switching the look with the crew in the shop leaks no materials / cycle " + cycle + " " + materials + " vs " + materialsOn);
            }
            // The crew still work after the switches: Pip serves one more visitor.
            yield return VisitorQueues("after the switches");
            var visit = queuedVisit;
            yield return StaffEnter(); Check(shop.SetCashierPaused(false), "Pip serves after the switches");
            shop.SetPanel(ShopPanel.None);
            yield return CrewWait(() => visit.Outcome == CustomerOutcome.Sold, 15, "Pip's sale after the switches");
            yield return CrewWait(() => shop.Customers.ActiveCount == 0, 20, "the customer leaves");
            yield return StaffEnter(); Check(shop.SetCashierPaused(true), "Hold Pip");
            shop.SetPanel(ShopPanel.None);
        }

        // ---------------------------------------------------------------- comfort

        IEnumerator CharacterComfort(ShopWorld w)
        {
            var pip = shop.Cashier.Actor;
            var counter = StationOf(StationKind.Counter).transform;
            DioramaView(shop.Customers.Navigation.QueueSpot(0) + counter.forward * -.4f, pip.transform.position + Vector3.up * 1.45f);
            float peak = 0, start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < 4.2f) { peak = Mathf.Max(peak, pip.DioramaLidWeight); yield return null; }
            Check(peak > .75f, "Pip blinks on its own clock / lids " + peak.ToString("0.00"));
            Check(shop.SetComfortSettings(true, FxEffectDetail.Standard), "Reduced visual motion for the characters");
            yield return null;
            peak = 0; start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < 4.2f) { peak = Mathf.Max(peak, pip.DioramaLidWeight); yield return null; }
            Check(peak < .05f, "Reduced motion: no blinking / lids " + peak.ToString("0.00"));
            var visit = Spawn(CustomerArchetypes.Collector);
            var actor = shop.Customers.ActorOf(visit);
            yield return CrewWait(() => actor && actor.Phase == CustomerPhase.Browsing, 25, "a visitor browses with reduced motion");
            yield return new WaitForSeconds(.6f);
            Check(actor.DioramaMood == DioramaVisitorMood.Haggle && actor.DioramaVisitor.DioramaMouthWeight(DioramaVisitorMood.Haggle) > .7f,
                "Reduced motion keeps the faces' reactions (they are not motion)");
            shop.Customers.HoldVisitsForValidation = true;
            var away = -actor.transform.forward; away.y = 0;
            actor.transform.rotation = Quaternion.LookRotation(away.normalized) * Quaternion.Euler(0, -15, 0);
            yield return null;
            FaceView(actor.DioramaVisitor, 1.3f, .2f);
            yield return DioramaShot("character-reduced");
            shop.Customers.SendVisitorsAwayForValidation();
            shop.Customers.HoldVisitsForValidation = false;
            Check(shop.SetComfortSettings(false, FxEffectDetail.Standard), "Back to standard comfort");
            yield return null; yield return null;
        }

        // ---------------------------------------------------------------- the character studies

        string MoodText(ShopUiStrings strings, DioramaVisitorMood mood, bool crew) =>
            strings.Get("study.mood." + mood + (crew && mood == DioramaVisitorMood.Shocked ? ".crew" : ""));

        IEnumerator CharacterStudy(ShopWorld w)
        {
            var root = shop.ToolkitUi.Root;
            var strings = shop.ToolkitUi.Strings;
            shop.SetPanel(ShopPanel.Pause);
            yield return HudFrames(3);
            yield return CardClick(root.Q<Button>(PauseView.PresentationName), "Presentation sample");
            yield return HudFrames(3);
            foreach (int view in new[] { 3, 4 })
            {
                bool crew = view == 4;
                var roles = crew ? DioramaRoles.CrewRoles : DioramaRoles.Customers;
                shop.SelectPresentationView(view);
                yield return HudFrames(3);
                Check(shop.PresentationCharacters && shop.PresentationCrewView == crew && CardShown(root, StudyPresenter.SpeciesName) &&
                    CardShown(root, StudyPresenter.TrickName) && !CardShown(root, StudyPresenter.VariantName),
                    "The character study offers the next " + (crew ? "crew member" : "customer type") + " and their reactions with the look on");
                for (int i = 0; i < roles.Count; i++)
                {
                    string roleId = roles[i];
                    DioramaRoles.TryGet(roleId, out var role);
                    if (i > 0) yield return CardClick(root.Q<Button>(StudyPresenter.SpeciesName), crew ? "Next crew" : "Next type");
                    yield return HudFrames(2);
                    var figure = w.StudyCharacterForValidation(crew);
                    Check(shop.PresentationRoleId == roleId && figure && figure.IsDiorama && figure.DioramaRole == role, "The study shows the kit " + roleId);
                    Check(StudyText(root, StudyPresenter.TitleName) == strings.Get("study.role." + roleId),
                        "The study names the " + roleId + " / " + StudyText(root, StudyPresenter.TitleName));
                    Check(StudyText(root, StudyPresenter.NoteName) == strings.Get("study.role.note." + roleId), "The study describes the " + roleId);
                    if (roleId == "cashier") Check(figure.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Step stool"), "Pip keeps the step stool in the study");
                    yield return new WaitForSeconds(.4f);
                    yield return DioramaShot("study-" + roleId);
                    CheckStudy("Character study, " + roleId);
                    var reactions = DioramaRoles.Reactions(role);
                    for (int r = 0; r < reactions.Count; r++)
                    {
                        yield return CardClick(root.Q<Button>(StudyPresenter.TrickName), "Reaction");
                        yield return HudFrames(2);
                        var mood = reactions[r];
                        figure = w.StudyCharacterForValidation(crew);
                        Check(shop.PresentationMood == mood && w.StudyMoodForValidation == mood, "Reaction steps to " + mood + " / " + roleId);
                        yield return new WaitForSeconds(.45f);
                        // Concentrating is a half-open mouth (0.55 of the Haggle shape); the rest go all the way.
                        Check(figure.DioramaMouthWeight(mood) > (mood == DioramaVisitorMood.Focus ? .5f : .8f), "The study face shows " + mood + " / " + roleId + " / " + figure.DioramaMouthWeight(mood).ToString("0.00"));
                        if ((mood == DioramaVisitorMood.Haggle || mood == DioramaVisitorMood.Focus) && role.Squint > 0)
                            Check(figure.DioramaLidWeight >= role.Squint * (mood == DioramaVisitorMood.Focus ? 1 : .9f) - .02f, "It squints its own way / " + roleId + " / " + mood);
                        Check(StudyText(root, StudyPresenter.TitleName) == strings.Format("study.character.title", strings.Get("study.role." + roleId), MoodText(strings, mood, crew)),
                            "The study names the reaction / " + roleId + " / " + mood);
                        if (roleId == "casual" || roleId == "cashier" || r == 0)
                        {
                            yield return DioramaShot("study-" + roleId + "-" + mood.ToString().ToLowerInvariant());
                            CheckStudy("Character study, " + roleId + " " + mood);
                        }
                    }
                    yield return CardClick(root.Q<Button>(StudyPresenter.TrickName), "Reaction");
                    yield return HudFrames(2);
                    Check(shop.PresentationMood == DioramaVisitorMood.Content, "Reaction steps back to none / " + roleId);
                }
                yield return CardClick(root.Q<Button>(StudyPresenter.SpeciesName), crew ? "Next crew" : "Next type");
                yield return HudFrames(2);
                Check(shop.PresentationRoleId == roles[0], "Next wraps round to the first / " + (crew ? "crew" : "customers"));
            }
            // The look off: the studies show the classic figures with no character controls.
            shop.SetDioramaLook(false);
            yield return HudFrames(3);
            Check(!shop.PresentationCharacters && !CardShown(root, StudyPresenter.SpeciesName), "Look off: the classic study, with no character controls");
            var classic = w.StudyCharacterForValidation(true);
            Check(classic && !classic.IsDiorama, "Look off: the classic crew figure in the study");
            shop.SetDioramaLook(true);
            yield return HudFrames(3);
            Check(shop.PresentationCharacters && w.StudyCharacterForValidation(true) && w.StudyCharacterForValidation(true).IsDiorama, "Look on again: the kit study figure is back");
            yield return CardClick(root.Q<Button>(StudyPresenter.BackName), "Back to pause");
            Check(shop.Panel == ShopPanel.Pause, "Back to pause from the character study");
            shop.SetPanel(ShopPanel.None);
            yield return HudFrames(2);
        }

        // ---------------------------------------------------------------- timings

        /// <summary>AD.7: frame cost at the counter with the three crew and as many visitors as the shop admits at once (one while
        /// the first-sale accommodation holds), the look on and off.</summary>
        IEnumerator CharacterTimings(ShopWorld w)
        {
            dioramaTiming.device = SystemInfo.deviceModel; dioramaTiming.gpu = SystemInfo.graphicsDeviceName; dioramaTiming.os = SystemInfo.operatingSystem;
            dioramaTiming.quality = QualitySettings.names[QualitySettings.GetQualityLevel()]; dioramaTiming.width = Screen.width; dioramaTiming.height = Screen.height;
            dioramaTiming.targetFrameRate = Application.targetFrameRate; dioramaTiming.vsync = QualitySettings.vSyncCount; dioramaTiming.frameTimingEnabled = FrameTimingManager.IsFeatureEnabled();
            var counter = StationOf(StationKind.Counter).transform;
            var eye = counter.TransformPoint(new Vector3(.4f, .2f, -4.2f)); var target = counter.TransformPoint(new Vector3(0, 1.1f, 0));
            foreach (bool look in new[] { true, false, true, false })
            {
                if (shop.DioramaLookOn != look) shop.SetDioramaLook(look);
                // Visitors keep the look they arrived with, so two arrive afresh for each sample.
                shop.Customers.SendVisitorsAwayForValidation(); yield return null;
                Spawn(CustomerArchetypes.Premium);
                if (shop.Customers.EffectiveMaxVisitors > 1) Spawn(CustomerArchetypes.Bargain);
                int visitors = shop.Customers.ActiveCount;
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
                dioramaTiming.samples.Add(new DioramaTimingSample { view = "counter, the crew in the shop with " + visitors + (visitors == 1 ? " visitor" : " visitors"), look = look, frames = interval.Count, gpuFrames = gpu.Count,
                    cpuP50 = P(cpu, .5), cpuP95 = P(cpu, .95), gpuP50 = P(gpu, .5), gpuP95 = P(gpu, .95), intervalP50 = P(interval, .5), intervalP95 = P(interval, .95) });
                if (look) yield return DioramaShot("characters-counter-timing");
            }
            if (!shop.DioramaLookOn) shop.SetDioramaLook(true);
            shop.Customers.SendVisitorsAwayForValidation();
            Check(dioramaTiming.samples.Count == 4 && dioramaTiming.samples.All(s => s.frames == 240), "Frame timings recorded with the look on and off");
            File.WriteAllText(Path.Combine(evidence, "diorama-timing.json"), JsonUtility.ToJson(dioramaTiming, true));
        }
    }
}
#endif
