using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Expeditions;

// Royalty's problem causers: an outpost or mech cluster with a machine that bends the weather or the mind. A win
// destroys the machine and ends what it causes; a failure leaves it all where it was.
public class SiteLinkWorker_ProblemCauser : SiteLinkWorker
{
    public override void Conclude(Site s, Quest? q, Outcome outcome, ExpeditionReport report, List<Pawn> raiders)
    {
        if (outcome == Outcome.Failure) return;
        report.successText ??= def.successText;
        // The quest first, so destroying the causer below can't count as its own "destroyed" signal.
        q?.End(QuestEndOutcome.Success, sendLetter: false);
        foreach (SitePart part in s.parts)
        {
            if (part.conditionCauser is not { } causer) continue;
            // The causer only ticks with its site, so its conditions would lapse on their own; end them now.
            if (causer.TryGetComp<CompCauseGameCondition>() is { } comp)
                foreach (GameCondition c in comp.CausedConditions.ToList()) c.End();
            if (!causer.Destroyed) causer.Destroy(DestroyMode.QuestLogic);
        }
        if (Props.raidCredit) IdeoUtility.Notify_PlayerRaidedSomeone(raiders);
        if (!s.Destroyed) s.Destroy();
    }
}
