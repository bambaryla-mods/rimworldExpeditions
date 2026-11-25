using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Expeditions;

// What can happen to a squad on the road, checked from the 250-tick loop: a member pulled away by someone else
// (a hiree whose faction turned on the colony, another mod's pawn), a creepjoiner whose downside comes due, and
// slaves left with nobody to keep them in line.
public static class SquadEvents
{
    private static readonly HashSet<int> failed = new();
    private static bool fought; // a fight happened this tick

    // True when the expedition ended or turned around this tick, so the caller skips its phase step.
    public static bool Tick(Expedition e)
    {
        if (e.phase == Phase.Departing) return false;
        int now = Find.TickManager.TicksGame;
        Rand.PushState(e.opp.Seed ^ now);
        fought = false;
        try
        {
            TrackHirees(e);
            bool happened = Pulled(e) | Creepjoiners(e, now);
            if (!happened) return false;
            // Whatever happened, slaves may now be on their own, or nobody may be left to lead the way.
            CheckOverlord(e);
            if (!ExpeditionsGame.Instance.active.Contains(e)) return true;
            if (!People(e).Any())
            {
                if (fought)
                {
                    // Killed on the road: lost, animals and all, like a squad wiped out at the site.
                    ExpeditionReport report = EndEarly(e);
                    report.lostText ??= "Exp_LostText".Translate(e.opp.Label);
                    Arrival.Return(e);
                    return true;
                }
                // Only called or walked away: the animals and the ride find their own way home.
                e.unescorted = true;
                if (e.phase != Phase.Returning) EndEarly(e);
            }
            return true;
        }
        catch (Exception ex)
        {
            if (failed.Add(e.id)) Log.Error($"[Expeditions] Squad event failed for expedition {e.id}: {ex}");
            return !ExpeditionsGame.Instance.active.Contains(e);
        }
        finally
        {
            Rand.PopState();
        }
    }

    // Trips from before hirees were tracked at launch, and anyone hired since: Comm Recruiter still has a record
    // for them until it recalls them.
    private static void TrackHirees(Expedition e)
    {
        foreach (Pawn p in e.squad)
            if (!e.hirees.Contains(p) && e.contents.Contains(p) && CommRecruiterCompat.TryGetHiree(p, out _, out _)) e.hirees.Add(p);
    }

    // ---------------------------------------------------------------- slaves

    public static void CheckOverlord(Expedition e)
    {
        var held = Living(e).ToList();
        var slaves = held.Where(p => p.IsSlave).ToList();
        if (slaves.Count == 0 || held.Any(Eligibility.IsOverlord)) return;
        foreach (Pawn s in slaves) RunAway(e, s);
        e.Note("Exp_SlavesFled".Translate());
    }

    // Once, when the expedition resolves. Suppression is frozen while away, so this is the value they left with.
    public static void SlaveRoll(Expedition e)
    {
        foreach (Pawn s in Living(e).Where(p => p.IsSlave).ToList())
        {
            float suppression = s.needs?.TryGetNeed<Need_Suppression>()?.CurLevel ?? 1f;
            if (Rand.Value < ExpeditionsDefOf.Tuning.SlaveRunawayCurve.Evaluate(suppression)) RunAway(e, s);
        }
    }

    // A slave gets away: no longer the colony's, and back with whoever they were taken from, if they still exist.
    // Done by hand because vanilla's release needs a map to walk off. SetFaction also clears the slave status,
    // so the old faction is read first.
    public static void RunAway(Expedition e, Pawn p)
    {
        e.Forget(p);
        Faction? to = p.SlaveFaction is { IsPlayer: false, defeated: false } f ? f : null;
        p.SetFaction(to);
        ToWorld(p);
        e.NoteFate(p, PawnFate.RanAway);
    }

    // ---------------------------------------------------------------- pulled away

    // True when someone left the squad this tick.
    private static bool Pulled(Expedition e)
    {
        var missing = e.squad.Where(p => !e.contents.Contains(p)).ToList();
        if (missing.Count == 0) return false;
        // Not pulled away: someone who died in the holder (swapped for a corpse), or, in saves from before every
        // removal went through Forget, someone already killed or taken at the site.
        foreach (Pawn p in missing.Where(p => p.Dead || CaptiveTracker.IsHeld(p)
                     || e.report?.pawns.Any(r => r.pawn == p && r.fate is PawnFate.Killed or PawnFate.Captured) == true).ToList())
        {
            e.Forget(p);
            missing.Remove(p);
        }
        if (missing.Count == 0) return false;

        var hirees = missing.Where(e.hirees.Contains).ToList();
        var hostile = hirees.Where(p => !p.Dead && p.Faction != null && p.Faction.HostileTo(Faction.OfPlayer)).ToList();
        foreach (Pawn p in missing) e.Forget(p);
        foreach (Pawn p in missing.Except(hostile)) e.Note("Exp_PawnPulled".Translate(p.LabelShortCap));
        if (hirees.Count == 0) return true; // someone else's pawn: noted, and the trip goes on

        // A hiree recalled mid-trip ends the trip. Hostile ones don't leave empty-handed.
        if (hostile.Count > 0) Betrayal(e, hostile);
        if (ExpeditionsGame.Instance.active.Contains(e)) EndEarly(e);
        return true;
    }

    private static void Betrayal(Expedition e, List<Pawn> hirees)
    {
        var tuning = ExpeditionsDefOf.Tuning;
        string names = hirees.Select(p => p.LabelShortCap).ToCommaList(true);
        Faction faction = hirees[0].Faction;

        if (!People(e).Any())
        {
            ExpeditionReport report = EndEarly(e);
            string line = "Exp_HireesTookAll".Translate(names, faction.Name, e.opp.Label);
            report.lostText = line;
            report.events.Add(line);
            report.loot.Clear();
            foreach (Pawn h in hirees) report.SetFate(h, PawnFate.RanAway);
            Arrival.Return(e);
            return;
        }

        // First they try to slip away with what they can carry; only if they are caught is there a fight.
        double stealth = SquadEvaluator.Score(hirees, ExpeditionsDefOf.Exp_Stealth);
        if (Rand.Value < tuning.RatioCurve.Evaluate(stealth / Math.Max(1f, tuning.slipAwayRequirement)))
        {
            float capacity = hirees.Sum(p => MassUtility.Capacity(p, null));
            List<string> taken = Take(e, capacity);
            e.Note("Exp_HireesSlipped".Translate(names, taken.Count > 0 ? taken.ToCommaList(true) : (string)"Exp_Nothing".Translate()));
            foreach (Pawn h in hirees) e.NoteFate(h, PawnFate.RanAway);
            return;
        }

        e.Note("Exp_HireesCaught".Translate(names));
        if (Fight(e, SquadEvaluator.Score(hirees, ExpeditionsDefOf.Exp_Fight)))
        {
            foreach (Pawn h in hirees)
            {
                if (Rand.Bool)
                {
                    KillAway(h);
                    e.NoteFate(h, PawnFate.Killed);
                }
                else e.NoteFate(h, PawnFate.RanAway);
            }
            return;
        }

        // They won: animals, the ride and the cargo go with them, and the survivors walk.
        foreach (Pawn h in hirees) e.NoteFate(h, PawnFate.RanAway);
        double leg = LegDone(e);
        Phase before = e.phase;
        foreach (Thing t in e.contents.ToList())
        {
            if (t is Pawn p && p.RaceProps.Humanlike && !RideFinder.VehicleCompat.IsVehicle(p)) continue;
            if (t is Pawn animal && !RideFinder.VehicleCompat.IsVehicle(animal))
            {
                e.Forget(animal);
                if (!animal.Dead) animal.SetFaction(faction);
                ToWorld(animal);
                continue;
            }
            if (t is Pawn vehicle) e.Forget(vehicle);
            else
            {
                e.contents.Remove(t);
                OffLoot(e.report, t.def, t.stackCount);
            }
            if (!t.Destroyed) t.Destroy();
        }
        Take(e, float.MaxValue); // and whatever was in the survivors' packs
        e.rideThings.Clear();
        e.ride = Ride.Foot;
        e.Note("Exp_HireesWon".Translate(names));
        WalkHome(e, before, leg);
    }

    // How far through the current phase the squad is, 0 to 1.
    private static double LegDone(Expedition e) => e.PhaseProgress;

    // The ride is gone: whatever is left of the way home takes walking time. Called with the phase and progress
    // from before the trip was cut short.
    private static void WalkHome(Expedition e, Phase before, double legDone)
    {
        if (e.phase != Phase.Returning && ExpeditionsGame.Instance.active.Contains(e)) EndEarly(e);
        if (!ExpeditionsGame.Instance.active.Contains(e)) return;
        double foot = Math.Max(e.tripFootDays, 0);
        double days = before switch
        {
            Phase.Outbound => legDone * foot,
            Phase.OnSite => foot,
            _ => (1 - legDone) * foot,
        };
        e.StartPhase(Phase.Returning, (int)(days * GenDate.TicksPerDay));
    }

    // Food first, then whatever is worth most, from the squad's packs and the loose cargo, until the weight is used
    // up. What was taken comes off the report's loot list too.
    private static List<string> Take(Expedition e, float capacityKg)
    {
        var sources = People(e).SelectMany(p => (p.inventory?.innerContainer ?? Enumerable.Empty<Thing>()).ToList())
            .Concat(e.contents.Where(t => t is not Pawn && t is not Building).ToList())
            .OrderByDescending(t => t.def.IsNutritionGivingIngestible)
            .ThenByDescending(t => t.MarketValue)
            .ToList();
        var taken = new List<string>();
        foreach (Thing t in sources)
        {
            if (capacityKg <= 0f) break;
            float mass = t.GetStatValue(StatDefOf.Mass);
            int n = mass <= 0f ? t.stackCount : (int)Math.Min(t.stackCount, capacityKg / mass);
            if (n <= 0) continue;
            capacityKg -= n * mass;
            Thing gone = n >= t.stackCount ? t : t.SplitOff(n);
            taken.Add(gone.LabelCap);
            OffLoot(e.report, gone.def, gone.stackCount);
            gone.holdingOwner?.Remove(gone);
            if (!gone.Destroyed) gone.Destroy();
        }
        return taken;
    }

    private static void OffLoot(ExpeditionReport? report, ThingDef def, int count)
    {
        if (report == null) return;
        foreach (ThingDefCountClass l in report.loot.Where(l => l.thingDef == def).ToList())
        {
            int off = Math.Min(count, l.count);
            l.count -= off;
            count -= off;
            if (l.count <= 0) report.loot.Remove(l);
            if (count <= 0) return;
        }
    }

    // ---------------------------------------------------------------- creepjoiners

    // True when a creepjoiner's downside fired this tick.
    private static bool Creepjoiners(Expedition e, int now)
    {
        if (!ExpeditionsGame.Instance.active.Contains(e)) return false;
        foreach (Pawn p in e.AwayPawns.ToList())
        {
            CreepDue due = CreepJoinerCompat.Due(p, now);
            if (due == CreepDue.None) continue;
            CreepJoinerCompat.MarkFired(p, due);
            e.Forget(p);
            if (due == CreepDue.Leaves)
            {
                if (p.Faction == Faction.OfPlayer) p.SetFaction(null);
                ToWorld(p);
                e.NoteFate(p, PawnFate.RanAway);
                e.Note("Exp_CreepLeft".Translate(p.LabelShortCap));
            }
            else Turned(e, p, due);
            return true;
        }
        return false;
    }

    private static void Turned(Expedition e, Pawn p, CreepDue kind)
    {
        var tuning = ExpeditionsDefOf.Tuning;
        double own = SquadEvaluator.Score(new[] { p }, ExpeditionsDefOf.Exp_Fight);
        double enemy;
        switch (kind)
        {
            case CreepDue.Fleshbeasts:
                enemy = tuning.fleshbeastFight;
                e.Note("Exp_CreepFleshbeasts".Translate(p.LabelShortCap));
                break;
            case CreepDue.Sightstealers:
                Map? home = e.originMap != null && Find.Maps.Contains(e.originMap) ? e.originMap : Find.AnyPlayerHomeMap;
                float points = home != null ? StorytellerUtility.DefaultThreatPointsNow(home) : 100f;
                enemy = SiteLinkMath.FightRequirement(points * tuning.sightstealerPointsFactor, 1, 100) + own;
                e.Note("Exp_CreepSightstealers".Translate(p.LabelShortCap));
                break;
            default:
                enemy = own * tuning.turnedCreepjoinerFactor;
                e.Note("Exp_CreepAssault".Translate(p.LabelShortCap));
                break;
        }

        bool won = Fight(e, enemy);
        // No longer one of the colony's, whatever happens next: a hostile, not a colonist, dies here.
        if (!p.Dead) p.SetFaction(Faction.OfEntities);
        if (kind == CreepDue.Fleshbeasts || won)
        {
            KillAway(p);
            e.NoteFate(p, PawnFate.Killed);
        }
        else
        {
            ToWorld(p);
            e.NoteFate(p, PawnFate.RanAway);
        }
        EndEarly(e);
    }

    // ---------------------------------------------------------------- shared

    // The squad's people fight off an enemy of the given Fight strength. Wounds and deaths scale with how
    // one-sided it was. Returns whether the squad won.
    public static bool Fight(Expedition e, double enemy)
    {
        var tuning = ExpeditionsDefOf.Tuning;
        var fighters = People(e).ToList();
        if (fighters.Count == 0) return false;
        fought = true;
        double ratio = SquadMath.FightRatio(SquadEvaluator.Score(fighters, ExpeditionsDefOf.Exp_Fight), enemy);
        bool won = SquadMath.SquadWins(ratio, Rand.Value, tuning.FightWinCurve);
        double death = tuning.FightDeathCurve.Evaluate(ratio);
        double wound = tuning.FightWoundCurve.Evaluate(ratio);
        foreach (Pawn p in fighters)
        {
            if (Rand.Value < death)
            {
                Resolver.Kill(e, p);
                e.NoteFate(p, PawnFate.Killed);
                continue;
            }
            float severity = Mathf01((float)wound * Rand.Range(0.7f, 1.3f));
            List<string> injuries = InjuryMaker.Apply(p, severity, severity > 0.5f);
            if (injuries.Count > 0) e.NoteFate(p, PawnFate.Injured, injuries);
        }
        e.Note((won ? "Exp_FightWon" : "Exp_FightLost").Translate());
        return won;
    }

    // Outbound or on site: turn for home without doing the job. Already on the way home: nothing changes.
    public static ExpeditionReport EndEarly(Expedition e)
    {
        if (e.phase is Phase.Outbound or Phase.OnSite) return SiteLinks.TurnBack(e, Outcome.CutShort);
        if (e.report == null)
        {
            e.report = new ExpeditionReport { siteLabel = e.opp.Label, tick = Find.TickManager.TicksGame, outcome = Outcome.CutShort, hasFinds = false };
            e.TakePending(e.report);
        }
        return e.report;
    }

    private static IEnumerable<Pawn> Living(Expedition e) => e.AwayPawns.Where(p => !p.Dead);

    private static IEnumerable<Pawn> People(Expedition e) =>
        Living(e).Where(p => p.RaceProps.Humanlike && !RideFinder.VehicleCompat.IsVehicle(p) && !e.IsCaptive(p));

    private static void ToWorld(Pawn p)
    {
        if (!p.Dead && !Find.WorldPawns.Contains(p)) Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.KeepForever);
    }

    private static void KillAway(Pawn p)
    {
        if (p.Dead) return;
        if (!Find.WorldPawns.Contains(p)) Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.KeepForever);
        p.Kill(null);
    }

    private static float Mathf01(float f) => f < 0 ? 0 : f > 1 ? 1 : f;
}
