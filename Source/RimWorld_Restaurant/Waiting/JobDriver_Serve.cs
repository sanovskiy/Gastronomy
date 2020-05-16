using System.Collections.Generic;
using Restaurant.Dining;
using Verse;
using Verse.AI;

namespace Restaurant.Waiting
{
    public class JobDriver_Serve : JobDriver
    {
        private Pawn Patron => job.GetTarget(TargetIndex.A).Pawn;
        private Thing Food => job.GetTarget(TargetIndex.B).Thing;
        private IntVec3 DiningSpot => job.GetTarget(TargetIndex.C).Cell;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            var food = Food;
            var patron = Patron;
            var patronJob = patron.GetDriver<JobDriver_Dine>();
            var diningSpot = patronJob?.DiningSpot;

            var order = patron?.GetRestaurant().GetOrderFor(patron);
            if (order == null)
            {
                Log.Message($"{patron.NameShortColored} has no existing order.");
                return false;
            }

            if (patron.GetRestaurant().IsBeingDelivered(order, pawn))
            {
                Log.Message($"{pawn.NameShortColored}: Order for {patron.NameShortColored} is already being delivered.");
                return false;
            }

            if (diningSpot == null)
            {
                Log.Message($"{pawn.NameShortColored} couldn't serve {patron?.NameShortColored}: patronJob = {patron.jobs.curDriver?.GetType().Name}");
                return false;
            }

            if (!pawn.Reserve(food, job, 1, 1, null, errorOnFailed))
            {
                Log.Message($"{pawn.NameShortColored} FAILED to reserve food {food?.Label}.");
                return false;
            }

            order.consumable = food;

            Log.Message($"{pawn.NameShortColored} reserved food {food.Label}.");
            job.count = 1;
            job.SetTarget(TargetIndex.C, diningSpot);
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            var wait = Toils_General.Wait(50, TargetIndex.A).FailOnNotDiningQueued(TargetIndex.A);

            //this.FailOnNotDining(TargetIndex.A);
            this.FailOnDestroyedOrNull(TargetIndex.B);
            this.FailOnForbidden(TargetIndex.B);
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch);
            yield return Toils_Haul.StartCarryThing(TargetIndex.B);
            yield return WaitingUtility.UpdateOrderConsumableTo(TargetIndex.A, TargetIndex.B);
            yield return WaitingUtility.FindRandomAdjacentCell(TargetIndex.A, TargetIndex.C); // A is the patron, C is the spot
            yield return Toils_Haul.CarryHauledThingToCell(TargetIndex.C);
            yield return wait;
            yield return Toils_Jump.JumpIf(wait, () => pawn.jobs.curJob?.GetTarget(TargetIndex.A).Pawn?.GetDriver<JobDriver_Dine>()==null); // Driver not available
            yield return WaitingUtility.GetDiningSpot(TargetIndex.A, TargetIndex.C);
            yield return Toils_Haul.CarryHauledThingToCell(TargetIndex.C);
            yield return Toils_Jump.JumpIf(wait, () => pawn.jobs.curJob?.GetTarget(TargetIndex.A).Pawn?.GetDriver<JobDriver_Dine>()==null); // Driver not available
            yield return WaitingUtility.ClearOrder(TargetIndex.A, TargetIndex.B);
        }
    }
}