using RimWorld;
using Verse.AI;
using Verse;

namespace Expeditions;

// On every comms console (added by DefInjector): the way into the Expeditions window.
[StaticConstructorOnStartup]
public class CompExpeditionsConsole : ThingComp
{
    private static readonly UnityEngine.Texture2D Icon = ContentFinder<UnityEngine.Texture2D>.Get("UI/Commands/FormCaravan");

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        foreach (Gizmo g in base.CompGetGizmosExtra()) yield return g;
        if (parent.Faction != Faction.OfPlayer) yield break;
        yield return new Command_Action
        {
            defaultLabel = "Exp_GizmoLabel".Translate(),
            defaultDesc = "Exp_GizmoDesc".Translate(),
            icon = Icon,
            action = () =>
            {
                Find.WindowStack.TryRemove(typeof(Window_Expeditions));
                Find.WindowStack.Add(new Window_Expeditions(parent));
            },
        };
    }

    public static void StartProspect(Pawn pawn, Building_CommsConsole console, ThingDef item)
    {
        Job job = JobMaker.MakeJob(ExpeditionsDefOf.Exp_Prospect, console);
        job.plantDefToSow = item;
        pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
    }
}

[StaticConstructorOnStartup]
public static class DefInjector
{
    static DefInjector()
    {
        foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
        {
            if (def.thingClass == null || !typeof(Building_CommsConsole).IsAssignableFrom(def.thingClass)) continue;
            def.comps ??= new List<CompProperties>();
            if (def.comps.Any(c => c.compClass == typeof(CompExpeditionsConsole))) continue;
            def.comps.Add(new CompProperties { compClass = typeof(CompExpeditionsConsole) });
        }
    }
}
