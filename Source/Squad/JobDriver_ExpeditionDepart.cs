using Verse;
using Verse.AI;

namespace Expeditions;

// Walk to the map edge (or to the ride) and board. The expedition id rides on job.count, so a save mid-departure
// resumes the walk.
public class JobDriver_ExpeditionDepart : JobDriver
{
    public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOn(() => ExpeditionsGame.Instance.ById(job.count) == null);
        yield return TargetA.HasThing
            ? Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch)
            : Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
        Toil board = ToilMaker.MakeToil("ExpeditionsBoard");
        board.initAction = () => Departure.Board(pawn, job.count);
        board.defaultCompleteMode = ToilCompleteMode.Instant;
        yield return board;
    }

    public override string GetReport() => "Exp_DepartReport".Translate();
}
