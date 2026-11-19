using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Expeditions;

// What happens at the site, rolled off-screen once the squad has spent its time there.
public static class Resolver
{
    // Gear wear by fate, as a fraction of max hit points.
    private const float WearUnharmed = 0.05f;
    private const float WearInjured = 0.2f;

    // Dev mode: force the next outcome.
    public static Outcome? forcedOutcome;

    public static void Resolve(Expedition e)
    {
        var game = ExpeditionsGame.Instance;
        ExpeditionSiteDef site = e.opp.site;
        var tuning = ExpeditionsDefOf.Tuning;
        var report = new ExpeditionReport { siteLabel = e.opp.Label, tick = Find.TickManager.TicksGame };

        Rand.PushState(e.opp.Seed);
        try
        {
            var pawns = e.AwayPawns.Where(p => !RideFinder.VehicleCompat.IsVehicle(p)).ToList();
            SquadEvaluation eval = SquadEvaluator.Evaluate(pawns, site, e.ride, RideFinder.Held(e), e.opp.Seed);
            Outcome outcome = forcedOutcome ?? OutcomeRoll.Roll(eval.success, Rand.Value, tuning.partialBand);
            forcedOutcome = null;
            bool found = outcome != Outcome.Failure && Rand.Value < eval.find;
            report.outcome = outcome;
            report.found = found;

            foreach (Pawn p in pawns) report.pawns.Add(ApplyFate(e, p, outcome, report));

            if (found && outcome == Outcome.Success && e.opp.captive is { } captive) Rescue(e, captive, report);
            foreach (Thing t in LootRoller.Roll(e, outcome, found, report)) e.contents.TryAdd(t);
            Reward(e, outcome);
        }
        finally
        {
            Rand.PopState();
        }

        e.report = report;
        int tiles = e.originMap != null ? Departure.Tiles(e.originMap, e.opp) : (int)Find.WorldGrid.ApproxDistanceInTiles(e.originTile, e.opp.tile);
        double back = TravelMath.BackDays(e.ride, e.tripFootDays, tiles, tuning.Travel);
        e.StartPhase(Phase.Returning, (int)(back * GenDate.TicksPerDay));
        if (!e.AwayPawns.Any(p => !p.Dead && p.RaceProps.Humanlike)) Arrival.Return(e);
        game.HolderChanged();
    }

    private static PawnResult ApplyFate(Expedition e, Pawn p, Outcome outcome, ExpeditionReport report)
    {
        ExpeditionSiteDef site = e.opp.site;
        PawnFate fate = OutcomeRoll.PickFate(site.risk.For(outcome), Rand.Value);
        // Pack animals are not taken; they come home hurt or not at all.
        if (!p.RaceProps.Humanlike && fate == PawnFate.Captured) fate = PawnFate.Killed;
        var result = new PawnResult { pawn = p, fate = fate };

        switch (fate)
        {
            case PawnFate.Unharmed:
                Wear(p, WearUnharmed);
                break;
            case PawnFate.Injured:
                result.injuries = InjuryMaker.Apply(p, Rand.Range(0.3f, 0.7f), outcome == Outcome.Failure);
                Wear(p, WearInjured);
                break;
            case PawnFate.Killed:
                Kill(e, p);
                return result;
            case PawnFate.Captured:
                if (!Capture(e, p)) goto case PawnFate.Injured;
                return result;
        }

        if (p.skills != null)
        {
            foreach (SiteChallenge sc in site.challenges)
            foreach (SkillDef skill in sc.challenge.skills)
            {
                if (p.skills.GetSkill(skill).TotallyDisabled) continue;
                float xp = ExpeditionsDefOf.Tuning.xpPerChallengeWeight * sc.weight;
                p.skills.Learn(skill, xp, direct: true);
                result.xp[skill] = (result.xp.TryGetValue(skill, out float had) ? had : 0f) + xp;
            }
        }
        return result;
    }

    private static void Wear(Pawn p, float fraction)
    {
        var gear = new List<Thing>();
        if (p.apparel != null) gear.AddRange(p.apparel.WornApparel);
        if (p.equipment != null) gear.AddRange(p.equipment.AllEquipmentListForReading);
        foreach (Thing t in gear.Where(t => t.def.useHitPoints))
            t.HitPoints = Math.Max(1, t.HitPoints - (int)Math.Ceiling(t.MaxHitPoints * fraction));
    }

    private static void Kill(Expedition e, Pawn p)
    {
        e.contents.Remove(p);
        if (!Find.WorldPawns.Contains(p)) Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.KeepForever);
        p.Kill(null);
        if (p.RaceProps.Humanlike)
            Find.LetterStack.ReceiveLetter("Exp_DeathLabel".Translate(p.LabelShortCap),
                "Exp_DeathText".Translate(p.LabelShortCap, e.opp.Label), LetterDefOf.Death);
    }

    // Taken by the site's faction, or any enemy faction. Vanilla's kidnapping then applies: they may be sold,
    // recruited, or turn up in raids.
    private static bool Capture(Expedition e, Pawn p)
    {
        Faction? captor = e.opp.faction is { } f && f.HostileTo(Faction.OfPlayer) ? f : Find.FactionManager.RandomEnemyFaction(false, false, true);
        if (captor?.kidnapped == null) return false;
        e.contents.Remove(p);
        captor.kidnapped.Kidnap(p, null);
        ExpeditionsGame.Instance.capturedTick[p] = Find.TickManager.TicksGame;
        Find.LetterStack.ReceiveLetter("Exp_CapturedLabel".Translate(p.LabelShortCap),
            "Exp_CapturedText".Translate(p.LabelShortCap, captor.Name, e.opp.Label), LetterDefOf.NegativeEvent);
        return true;
    }

    private static void Rescue(Expedition e, Pawn captive, ExpeditionReport report)
    {
        if (CaptiveTracker.Free(captive)) e.contents.TryAdd(captive);
        report.pawns.Add(new PawnResult { pawn = captive, fate = PawnFate.Unharmed });
    }

    private static void Reward(Expedition e, Outcome outcome)
    {
        int goodwill = e.opp.site.goodwillReward;
        if (goodwill <= 0 || e.opp.faction == null || outcome == Outcome.Failure) return;
        if (outcome == Outcome.Partial) goodwill /= 2;
        e.opp.faction.TryAffectGoodwillWith(Faction.OfPlayer, goodwill, true, true, null, null);
    }
}
