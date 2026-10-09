using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class ShopSession
    {
        const float WorkerCelebrationDelay = .35f, WorkerCelebrationExpiry = 1.5f;
        string pendingWorkerId, activeWorkerId;
        ShopCharacterVisual pendingWorkerActor, activeWorkerActor;
        float workerCelebrationWait, workerCelebrationExpiry;
        public string CelebratingWorkerId => activeWorkerId;
        internal string PendingWorkerCelebrationId => pendingWorkerId;

        // Only the completing worker calls this, after its successful transaction and claim cleanup.
        // A single expiring request lets service audio finish; work is never delayed for a joke.
        internal void QueueWorkerCelebration(string workerId)
        {
            if (pendingWorkerId != null || Comedy.Active || Comedy.CooldownRemaining > 0 ||
                !TryIdleWorker(workerId, out var actor) || !WorkerCelebrationVisible(actor)) return;
            pendingWorkerId = workerId;
            pendingWorkerActor = actor;
            workerCelebrationWait = WorkerCelebrationDelay;
            workerCelebrationExpiry = WorkerCelebrationExpiry;
        }

        bool TryIdleWorker(string workerId, out ShopCharacterVisual actor)
        {
            actor = null;
            if (!isActiveAndEnabled || Cycle == null) return false;
            if (workerId == ShopStaff.CashierId)
            {
                if (!Cashier || !Cashier.isActiveAndEnabled || Staff == null ||
                    !Staff.IsOnDuty(Cycle.DayNumber) || Cashier.CurrentJobId != null ||
                    !Customers || !Customers.isActiveAndEnabled || !Customers.Ready ||
                    !Customers.Navigation || !Customers.Navigation.isActiveAndEnabled || Customers.WaitingCount > 0 ||
                    Cycle.Phase != ShopDayPhase.Open) return false;
                actor = Cashier.Actor;
            }
            else if (workerId == ShopStaff.StockerId)
            {
                if (!Stocker || !Stocker.isActiveAndEnabled || Stocking == null ||
                    !Stocking.Employee.IsOnDuty(Cycle.DayNumber) || Stocker.CurrentJobId != null || Stocker.Blocked) return false;
                actor = Stocker.Actor;
            }
            else if (workerId == ShopStaff.TechnicianId)
            {
                if (!Technician || !Technician.isActiveAndEnabled || Laboratory == null ||
                    !Laboratory.Employee.IsOnDuty(Cycle.DayNumber) || Technician.CurrentJobId != null || Technician.Blocked) return false;
                actor = Technician.Actor;
            }
            return actor && actor.isActiveAndEnabled;
        }

        bool WorkerCelebrationVisible(ShopCharacterVisual actor) =>
            actor && Player && !Paused && Panel == ShopPanel.None && !PlacementActive &&
            Cycle.Phase != ShopDayPhase.Closing && !AmbientComedyDeferredForTutorial &&
            (Player.transform.position.z > .4f) == (actor.transform.position.z > .4f) &&
            Vector3.Distance(Player.transform.position, actor.transform.position) <= 7f;

        bool WorkerCelebrationStillValid(string id, ShopCharacterVisual expected) =>
            TryIdleWorker(id, out var current) && current == expected && WorkerCelebrationVisible(current);

        void TickWorkerCelebration(float seconds)
        {
            if (activeWorkerId != null)
            {
                if (!WorkerCelebrationStillValid(activeWorkerId, activeWorkerActor) ||
                    (sound && sound.ServicePriorityActive)) CancelComedy();
                return;
            }
            if (pendingWorkerId == null) return;
            workerCelebrationWait -= seconds;
            workerCelebrationExpiry -= seconds;
            if (workerCelebrationExpiry <= 0 || !WorkerCelebrationStillValid(pendingWorkerId, pendingWorkerActor))
            { ClearPendingWorkerCelebration(); return; }
            if (workerCelebrationWait > 0 || (sound && sound.ServicePriorityActive)) return;
            var workerId = pendingWorkerId;
            var actor = pendingWorkerActor;
            ClearPendingWorkerCelebration();
            // Shared/per-kind cooldown and concurrency apply to workers too. Rejections are dropped.
            if (!Comedy.TryStart(ComedyKind.WorkerCelebration, comedyVariant++, ReducedGags)) return;
            activeWorkerId = workerId;
            activeWorkerActor = actor;
            ComedyStartedCount++;
            if (sound) sound.PlayComedy(ComedyKind.WorkerCelebration, Comedy.Variant, HumorVolume);
        }

        internal void CancelWorkerCelebration(string workerId)
        {
            if (pendingWorkerId == workerId) ClearPendingWorkerCelebration();
            if (activeWorkerId == workerId) CancelComedy();
        }

        void ClearPendingWorkerCelebration()
        {
            pendingWorkerId = null; pendingWorkerActor = null;
            workerCelebrationWait = workerCelebrationExpiry = 0;
        }

        void ClearWorkerCelebration()
        {
            // Do not erase this frame's new work/carry pose when a job interrupted the gag.
            if (activeWorkerActor && !WorkerOwnsJob(activeWorkerId))
                activeWorkerActor.Pose(CharacterSamplePose.Idle, 0);
            activeWorkerId = null; activeWorkerActor = null;
            ClearPendingWorkerCelebration();
        }

        bool WorkerOwnsJob(string id) => id == ShopStaff.CashierId ? Cashier && Cashier.CurrentJobId != null :
            id == ShopStaff.StockerId ? Stocker && Stocker.CurrentJobId != null :
            id == ShopStaff.TechnicianId && Technician && Technician.CurrentJobId != null;

        string WorkerComedyCaption => activeWorkerId == ShopStaff.CashierId ? "Pip: " :
            activeWorkerId == ShopStaff.StockerId ? "Nori: " : activeWorkerId == ShopStaff.TechnicianId ? "Moss: " : "";
    }
}
