using UnityEngine;
using UnityEngine.Rendering;

namespace EvolutionShop
{
    /// <summary>
    /// One stationary checkout worker. The staff ledger owns employment and wages, the session
    /// owns active time, and the existing visit/model transaction owns all stock and payment.
    /// </summary>
    public sealed class ShopCashier : MonoBehaviour
    {
        public const float ServiceSeconds = 2.2f;
        public string CurrentJobId { get; private set; }
        public CustomerVisit Target { get; private set; }
        public float Progress => Mathf.Clamp01(elapsed / ServiceSeconds);
        public string Status => shop && shop.Paused ? "Game paused / cashier time frozen" : status;
        public ShopCharacterVisual Actor { get; private set; }
        public FxWorkerActivity WorkerActivity=>CurrentJobId!=null?FxWorkerActivity.Checkout:FxWorkerActivity.Idle;
        public float ServiceProgress=>Progress;

        ShopSession shop;
        Station counter;
        TextMesh caption;
        Renderer captionRenderer;
        Material captionMaterial, backingMaterial;
        Transform dutySign;
        Font font;
        float elapsed, poseTime;
        bool shown;
        string refusedJobId, captionLabel, status = "No cashier hired";

        public void Initialize(ShopSession session)
        {
            if (shop || !session) return;
            shop = session;
            foreach (var station in shop.World.Stations)
                if (station.Kind == StationKind.Counter) { counter = station; break; }
            if (counter) BuildActor();
            RefreshVisual();
        }

        /// <summary>Refresh after hiring, dismissal, schedule/payment changes, or reload.</summary>
        public void RefreshVisual()
        {
            ClearJob();
            bool hired = shop && shop.Staff != null && shop.Staff.Hired;
            // An explicit roster/reload refresh can recover a lost visual. Ordinary ticks cannot
            // silently replace a missing employee while a checkout is in progress.
            if (hired && !Actor && counter)
            {
                if (captionMaterial) Destroy(captionMaterial);
                if (backingMaterial) Destroy(backingMaterial);
                BuildActor();
            }
            if (Actor)
            {
                Actor.gameObject.SetActive(isActiveAndEnabled && hired);
                shown = hired;
                Actor.Pose(CharacterSamplePose.Idle, 0);
            }
            if (UpdateAvailability()) SetStatus("On duty / waiting for a queued customer", "ON DUTY / READY");
        }

        public void Tick(float seconds)
        {
            TickCore(seconds);
            if(shop&&Actor)Actor.PoseWorker(WorkerActivity,ServiceProgress,seconds,false,CurrentJobId,null,shop.Paused);
        }
        void TickCore(float seconds)
        {
            if (!shop || shop.Paused || !isActiveAndEnabled ||
                float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0) return;
            if (shown != (shop.Staff != null && shop.Staff.Hired)) RefreshVisual();
            if (!UpdateAvailability()) return;

            if (Target != null && !Eligible(Target))
            {
                ClearJob();
                SetStatus("Order changed / waiting for the next customer", "READY FOR CHECKOUT");
                return; // A replacement never inherits time spent on the old order.
            }
            if (Target == null)
            {
                var next = shop.Customers.NextToServe;
                if (!Eligible(next))
                {
                    refusedJobId = null;
                    if (next != null && next.Phase == CustomerPhase.Queued)
                    {
                        SetStatus("Waiting customer unavailable / checkout blocked", "CUSTOMER UNAVAILABLE");
                        return;
                    }
                    SetStatus("On duty / waiting for a queued customer", "ON DUTY / READY");
                    Animate(CharacterSamplePose.Idle, seconds);
                    return;
                }
                string jobId = ShopStaff.CashierId + "/checkout/" + next.Id;
                if (jobId == refusedJobId)
                {
                    SetStatus("Checkout blocked / serve manually or resume the cashier", "CHECKOUT BLOCKED");
                    return;
                }
                Target = next;
                CurrentJobId = jobId;
                elapsed = 0;
                SetStatus("Serving " + Target.ArchetypeName + " / agreed price " + Target.Price + " credits", "CHECKING OUT");
            }

            elapsed = Mathf.Min(ServiceSeconds, elapsed + seconds);
            Animate(CharacterSamplePose.Greet, seconds);
            if (elapsed < ServiceSeconds) return;

            // The command rechecks this exact visit, its reservation, and the staff duty state.
            // Poses and animation events cannot enter this payment path.
            var expected = Target;
            string committedJob = CurrentJobId;
            bool served = Eligible(expected) && shop.ServeCashier(expected);
            ClearJob();
            if (served) shop.QueueWorkerCelebration(ShopStaff.CashierId);
            if(!served)shop.PresentWorkerResult(Actor,FxWorkerEvent.Refused,committedJob,true);
            if (!served) refusedJobId = committedJob; // One attempt, then visible manual recovery.
            SetStatus(served ? "Checkout complete / ready for the next customer" :
                "Checkout blocked / serve manually or resume the cashier", served ? "THANK YOU!" : "CHECKOUT BLOCKED");
        }

        bool Eligible(CustomerVisit visit)
        {
            if (visit == null || !shop.Customers || !shop.Customers.Ready ||
                !shop.Customers.Navigation.isActiveAndEnabled ||
                visit.Phase != CustomerPhase.Queued || !ReferenceEquals(visit, shop.Customers.NextToServe)) return false;
            var actor = shop.Customers.ActorOf(visit);
            return actor && actor.isActiveAndEnabled && !actor.PathFailed;
        }

        bool UpdateAvailability()
        {
            if (!shop || shop.Staff == null) return Stop("Staff details unavailable", "OFF DUTY");
            if (!isActiveAndEnabled) return Stop("Cashier disabled", "OFF DUTY");
            if (!shop.Staff.Hired) return Stop("No cashier hired", "OFF DUTY");
            if (!Actor || !Actor.isActiveAndEnabled || !counter)
                return Stop("Cashier unavailable / reload to restore the employee", "WORKER UNAVAILABLE");
            if (!shop.Staff.Scheduled) return Stop("Off duty / not scheduled", "OFF DUTY / NOT SCHEDULED");
            if (shop.Staff.Paused) return Stop("Cashier paused / manual checkout is available", "WORK PAUSED");
            if (!shop.Staff.IsOnDuty(shop.Cycle.DayNumber)) return Stop("Off duty / pay the duty period at night", "OFF DUTY / UNPAID");
            if (shop.Cycle.Phase != ShopDayPhase.Open && shop.Cycle.Phase != ShopDayPhase.Closing)
                return Stop("Night preparation / ready when the shop opens", "READY FOR THE NEXT DAY");
            if (!shop.Customers || !shop.Customers.isActiveAndEnabled || !shop.Customers.Ready ||
                !shop.Customers.Navigation.isActiveAndEnabled)
                return Stop("Customer routes unavailable / checkout blocked", "CUSTOMER ROUTES BLOCKED");
            return true;
        }

        bool Stop(string explanation, string label)
        {
            ClearJob();
            SetStatus(explanation, label);
            return false;
        }

        /// <summary>Jobs are transient; releasing this timer never cancels a visitor's valid order.</summary>
        public void ResetTransient()
        {
            if(Actor&&shop&&shop.World)shop.World.CancelFxOwner(Actor.WorkerFxOwner);
            ClearJob();
            refusedJobId = null;
            poseTime = 0;
            if (UpdateAvailability()) SetStatus("On duty / waiting for a queued customer", "ON DUTY / READY");
        }

        void ClearJob()
        {
            if (shop) shop.CancelWorkerCelebration(ShopStaff.CashierId);
            Target = null;
            CurrentJobId = null;
            elapsed = 0;
            if (Actor) { Actor.Pose(CharacterSamplePose.Idle, 0); Actor.ClearWorkerJobPresentation(); }
        }

        void Animate(CharacterSamplePose pose, float seconds)
        {
            poseTime += seconds;
            Actor.Pose(pose, poseTime);
        }

        void SetStatus(string value, string label)
        {
            status = value;
            if (!caption || captionLabel == label) return;
            captionLabel = label;
            caption.text = "CASHIER\n" + label;
            FitCaption();
            FontAtlasRebuilt(font);
        }

        /// <summary>AD-C: the duty sign rides above the figure: Pip on the step stool stands taller than the classic cashier.</summary>
        public void FitSignToActor()
        {
            if (!dutySign) return;
            float height = Actor && Actor.IsDiorama && Actor.DioramaRole != null ? Mathf.Max(2.07f, Actor.DioramaRole.HeadTop + .3f) : 2.07f;
            dutySign.localPosition = new Vector3(0, height, .045f);
        }

        void BuildActor()
        {
            // The fixed counter faces local -Z. This full-size stationary rig stands behind it,
            // facing the queue. Only Idle/Greet are used; the preview parcel stays hidden.
            var actorRoot = new GameObject("Employed cashier / " + ShopStaff.CashierId).transform;
            actorRoot.SetParent(transform, false);
            actorRoot.position = counter.transform.TransformPoint(new Vector3(0, 0, .97f));
            actorRoot.rotation = counter.transform.rotation * Quaternion.Euler(0, 180, 0);
            Actor = actorRoot.gameObject.AddComponent<ShopCharacterVisual>();
            // AD-C: the crew member's kit figure while the diorama look is on (always shop teal), else the classic one.
            Actor.BuildForRole("cashier", true);
            Actor.ConfigureWorker(shop.World,ShopStaff.CashierId);

            var sign = new GameObject("Cashier duty sign").transform;
            sign.SetParent(actorRoot, false);
            dutySign = sign;
            FitSignToActor();
            sign.localRotation = Quaternion.Euler(0, 180, 0);
            backingMaterial = ShopWorld.MakeMaterial("Cashier / sign teal", new Color(.045f, .14f, .19f));
            ShopWorld.Shape(sign, "Duty sign backing", PrimitiveType.Cube, new Vector3(0, 0, .018f),
                new Vector3(1.45f, .33f, .025f), backingMaterial);
            var textRoot = new GameObject("Depth-tested cashier caption").transform;
            textRoot.SetParent(sign, false);
            caption = textRoot.gameObject.AddComponent<TextMesh>();
            captionLabel = null;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            caption.font = font;
            caption.fontSize = 80;
            caption.characterSize = .01f;
            caption.anchor = TextAnchor.MiddleCenter;
            caption.alignment = TextAlignment.Center;
            caption.color = new Color(1, .96f, .83f);
            var shader = Resources.Load<Shader>("EvolutionWorldText");
            if (!shader) throw new System.InvalidOperationException("Missing Resources/EvolutionWorldText.shader for cashier status.");
            captionMaterial = new Material(shader) { name = "Cashier / depth-tested caption" };
            captionRenderer = caption.GetComponent<Renderer>();
            captionRenderer.sharedMaterial = captionMaterial;
            captionRenderer.shadowCastingMode = ShadowCastingMode.Off;
            captionRenderer.receiveShadows = false;
            FontAtlasRebuilt(font);
        }

        void FitCaption()
        {
            if (!captionRenderer || string.IsNullOrEmpty(caption.text)) return;
            Vector3 size = captionRenderer.localBounds.size;
            if (size.x > .0001f && size.y > .0001f)
                caption.transform.localScale = Vector3.one * Mathf.Min(1.31f / size.x, .23f / size.y);
        }

        void FontAtlasRebuilt(Font rebuilt)
        {
            if (font && rebuilt == font && font.material && captionMaterial)
            { captionMaterial.mainTexture = font.material.mainTexture; FitCaption(); }
        }

        void OnEnable() { Font.textureRebuilt += FontAtlasRebuilt; if (shop) RefreshVisual(); }
        void OnDisable()
        {
            Font.textureRebuilt -= FontAtlasRebuilt;
            ResetTransient();
            if (Actor) Actor.gameObject.SetActive(false);
        }
        void OnDestroy()
        {
            Font.textureRebuilt -= FontAtlasRebuilt;
            if (Actor) Destroy(Actor.gameObject); // The rig disposes its own palette materials.
            if (captionMaterial) Destroy(captionMaterial);
            if (backingMaterial) Destroy(backingMaterial);
        }
    }
}
