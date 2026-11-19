using RimWorld;
using Verse;
using Verse.AI;

namespace Expeditions;

// A colonist at the comms console asking around for one item (carried on job.plantDefToSow). Progress is kept per
// item in ExpeditionsGame, so an interrupted job loses nothing and anyone can finish it.
public class JobDriver_Prospect : JobDriver
{
    private ThingDef Item => job.plantDefToSow;
    private Building_CommsConsole? Console => TargetA.Thing as Building_CommsConsole;

    public override bool TryMakePreToilReservations(bool errorOnFailed) =>
        pawn.Reserve(TargetA, job, 1, -1, null, errorOnFailed);

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedOrNull(TargetIndex.A);
        this.FailOn(() => Item == null || Console == null || !Console.CanUseCommsNow);
        this.FailOn(() => ProspectService.AskedThisWindow(Item));

        yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);

        Toil work = ToilMaker.MakeToil("ExpeditionsProspect");
        work.defaultCompleteMode = ToilCompleteMode.Never;
        work.handlingFacing = true;
        work.tickIntervalAction = delta =>
        {
            pawn.rotationTracker.FaceTarget(TargetA);
            var done = ExpeditionsGame.Instance.prospectTicksDone;
            done[Item] = (done.TryGetValue(Item, out int d) ? d : 0) + delta;
            if (done[Item] < ProspectService.TotalTicks(pawn)) return;
            ProspectService.Complete(Item, pawn.Map);
            ReadyForNextToil();
        };
        work.WithProgressBar(TargetIndex.A, () => ProspectService.Progress(Item, pawn));
        yield return work;
    }

    public override string GetReport() => "Exp_ProspectReport".Translate(Item?.label ?? "?");
}
