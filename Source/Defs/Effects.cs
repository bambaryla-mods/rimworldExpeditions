using RimWorld;
using Verse;

namespace Expeditions;

// What a site does to each pawn that comes through it, as <li Class="..."> on the site def. Runs at resolve, in the
// seeded Rand state, for humanlikes whose fate is Unharmed or Injured.
public abstract class PawnEffect
{
    public List<Outcome> outcomes = new() { Outcome.Success, Outcome.Partial, Outcome.Failure };

    public abstract void Apply(Pawn p, Expedition e, Outcome outcome, ExpeditionReport report);

    public virtual IEnumerable<string> ConfigErrors() { yield break; }
}

// What a site does once for its outcome. Apply runs at the site, after loot and rewards and outside the seeded Rand
// state: for what the deed itself earns. OnArrival runs when the squad is set down at home (map null when they became
// a caravan), before the report letter: for what depends on what came home. Not called for a squad lost on the road.
public abstract class OutcomeEffect
{
    public List<Outcome> outcomes = new() { Outcome.Success };

    public virtual void Apply(Expedition e, Outcome outcome, ExpeditionReport report) { }

    public virtual void OnArrival(Expedition e, Outcome outcome, Map? map, ExpeditionReport report) { }

    public virtual IEnumerable<string> ConfigErrors() { yield break; }
}

// Goodwill with every faction that can change it, except the listed ones. Halved on a partial win.
public class OutcomeEffect_GoodwillAll : OutcomeEffect
{
    public int amount;
    public bool includeHostile;
    public List<FactionDef> exceptFactionDefs = new();

    public override void Apply(Expedition e, Outcome outcome, ExpeditionReport report)
    {
        int n = outcome == Outcome.Partial ? HookMath.HalfUp(amount) : amount;
        if (n == 0) return;
        bool any = false;
        foreach (Faction f in Find.FactionManager.AllFactionsListForReading.ToList())
        {
            if (f.IsPlayer || f.Hidden || f.defeated || exceptFactionDefs.Contains(f.def)) continue;
            if (!includeHostile && f.HostileTo(Faction.OfPlayer)) continue;
            if (f.TryAffectGoodwillWith(Faction.OfPlayer, n, false, true, null, null)) any = true;
        }
        if (any) report.effectLines.Add("Exp_GoodwillAllExceptLine".Translate(n));
    }
}

// Calls into other mods' workers. A worker that throws is logged once per def and treated as having done nothing, so
// one bad hook never loses a squad or breaks the board.
public static class Effects
{
    private static readonly HashSet<string> logged = new();

    public static void Safe(Def owner, Action a)
    {
        try { a(); }
        catch (Exception ex) { LogOnce(owner, ex); }
    }

    public static T Safe<T>(Def owner, Func<T> f, T fallback)
    {
        try { return f(); }
        catch (Exception ex)
        {
            LogOnce(owner, ex);
            return fallback;
        }
    }

    private static void LogOnce(Def owner, Exception ex)
    {
        if (logged.Add(owner.defName)) Log.Error($"[Expeditions] A hook on {owner.defName} failed and was skipped: {ex}");
    }
}
