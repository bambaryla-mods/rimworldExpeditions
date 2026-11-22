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

    // True when the expedition ended or turned around this tick, so the caller skips its phase step.
    public static bool Tick(Expedition e)
    {
        if (e.phase == Phase.Departing) return false;
        int now = Find.TickManager.TicksGame;
        Rand.PushState(e.opp.Seed ^ now);
        try
        {
            return Pulled(e) || Creepjoiners(e, now);
        }
        catch (Exception ex)
        {
            if (failed.Add(e.id)) Log.Error($"[Expeditions] Squad event failed for expedition {e.id}: {ex}");
            return false;
        }
        finally
        {
            Rand.PopState();
        }
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
    // Done by hand because vanilla's release needs a map to walk off.
    public static void RunAway(Expedition e, Pawn p)
    {
        e.Forget(p);
        Faction? to = p.SlaveFaction is { IsPlayer: false, defeated: false } f ? f : null;
        p.guest?.SetGuestStatus(null);
        if (p.Faction != to) p.SetFaction(to);
        ToWorld(p);
        e.NoteFate(p, PawnFate.RanAway);
    }

    // ---------------------------------------------------------------- pulled away

    private static bool Pulled(Expedition e)
    {
        var missing = e.squad.Where(p => !e.contents.Contains(p)).ToList();
        if (missing.Count == 0) return false;
        var hirees = missing.Where(e.hirees.Contains).ToList();
        foreach (Pawn p in missing) e.Forget(p);

        foreach (Pawn p in missing.Except(hirees)) e.Note("Exp_PawnPulled".Translate(p.LabelShortCap));
        if (hirees.Count == 0)
        {
            CheckOverlord(e);
            return false;
        }

        // A hiree recalled mid-trip ends the trip. Hostile ones don't leave empty-handed.
        var hostile = hirees.Where(p => !p.Dead && p.Faction != null && p.Faction.HostileTo(Faction.OfPlayer)).ToList();
        foreach (Pawn p in hirees.Except(hostile)) e.Note("Exp_PawnPulled".Translate(p.LabelShortCap));
        if (hostile.Count > 0 && Betrayal(e, hostile)) return true;
        CheckOverlord(e);
        EndEarly(e);
        return true;
    }

    // True when the expedition is over (they took everything).
    private static bool Betrayal(Expedition e, List<Pawn> hirees)
    {
        var tuning = ExpeditionsDefOf.Tuning;
        string names = hirees.Select(p => p.LabelShortCap).ToCommaList(true);
        string faction = hirees[0].Faction.Name;

        if (!People(e).Any())
        {
            ExpeditionReport report = EndEarly(e);
            string line = "Exp_HireesTookAll".Translate(names, faction, e.opp.Label);
            report.lostText = line;
            report.events.Add(line);
            Arrival.Return(e);
            return true;
        }

        // First they try to slip away with what they can carry; only if they are caught is there a fight.
        double stealth = SquadEvaluator.Score(hirees, ExpeditionsDefOf.Exp_Stealth);
        if (Rand.Value < tuning.RatioCurve.Evaluate(stealth / Math.Max(1f, tuning.slipAwayRequirement)))
        {
            float capacity = hirees.Sum(p => MassUtility.Capacity(p, null));
            List<string> taken = TakeCarried(e, capacity);
            e.Note("Exp_HireesSlipped".Translate(names, taken.Count > 0 ? taken.ToCommaList(true) : (string)"Exp_Nothing".Translate()));
            foreach (Pawn h in hirees) e.NoteFate(h, PawnFate.RanAway);
            return false;
        }

        e.Note("Exp_HireesCaught".Translate(names));
        bool won = Fight(e, SquadEvaluator.Score(hirees, ExpeditionsDefOf.Exp_Fight));
        if (won)
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
            return false;
        }

        // They won: animals, the ride and the cargo go with them, and the survivors walk.
        foreach (Pawn h in hirees) e.NoteFate(h, PawnFate.RanAway);
        foreach (Thing t in e.contents.ToList())
        {
            if (t is Pawn p && p.RaceProps.Humanlike && !RideFinder.VehicleCompat.IsVehicle(p)) continue;
            if (t is Pawn animal && !RideFinder.VehicleCompat.IsVehicle(animal))
            {
                e.Forget(animal);
                if (!animal.Dead) animal.SetFaction(hirees[0].Faction);
                ToWorld(animal);
                continue;
            }
            if (t is Pawn vehicle) e.Forget(vehicle);
            else e.contents.Remove(t);
            if (!t.Destroyed) t.Destroy();
        }
        e.rideThings.Clear();
        e.ride = Ride.Foot;
        e.Note("Exp_HireesWon".Translate(names));
        ExpeditionsGame.Instance.HolderChanged();
        return false;
    }

    // Food first, then whatever is worth most, from the cargo and the squad's packs, until the weight is used up.
    private static List<string> TakeCarried(Expedition e, float capacityKg)
    {
        var sources = e.contents.Where(t => t is not Pawn && t is not Building).ToList()
            .Concat(People(e).SelectMany(p => (p.inventory?.innerContainer ?? Enumerable.Empty<Thing>()).ToList()))
            .OrderByDescending(t => t.def.IsNutritionGivingIngestible)
            .ThenByDescending(t => t.MarketValue)
            .ToList();
        var taken = new List<string>();
        foreach (Thing t in sources)
        {
            float mass = t.GetStatValue(StatDefOf.Mass);
            int n = mass <= 0f ? t.stackCount : Math.Min(t.stackCount, (int)(capacityKg / mass));
            if (n <= 0) continue;
            capacityKg -= n * mass;
            Thing gone = n >= t.stackCount ? t : t.SplitOff(n);
            taken.Add(gone.LabelCap);
            if (gone.holdingOwner != null) gone.holdingOwner.Remove(gone);
            gone.Destroy();
            if (capacityKg <= 0f) break;
        }
        return taken;
    }

    // ---------------------------------------------------------------- creepjoiners

    private static bool Creepjoiners(Expedition e, int now)
    {
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
                CheckOverlord(e);
                return false;
            }
            return Turned(e, p, due);
        }
        return false;
    }

    private static bool Turned(Expedition e, Pawn p, CreepDue kind)
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
        if (kind == CreepDue.Fleshbeasts || won)
        {
            KillAway(p);
            e.NoteFate(p, PawnFate.Killed);
        }
        else
        {
            if (!p.Dead) p.SetFaction(Faction.OfEntities);
            ToWorld(p);
            e.NoteFate(p, PawnFate.RanAway);
        }
        CheckOverlord(e);
        if (!People(e).Any())
        {
            EndEarly(e);
            Arrival.Return(e);
            return true;
        }
        EndEarly(e);
        return true;
    }

    // ---------------------------------------------------------------- shared

    // The squad's people fight off an enemy of the given Fight strength. Wounds and deaths scale with how
    // one-sided it was. Returns whether the squad won.
    public static bool Fight(Expedition e, double enemy)
    {
        var tuning = ExpeditionsDefOf.Tuning;
        var fighters = People(e).ToList();
        if (fighters.Count == 0) return false;
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
        Living(e).Where(p => p.RaceProps.Humanlike && !RideFinder.VehicleCompat.IsVehicle(p));

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
