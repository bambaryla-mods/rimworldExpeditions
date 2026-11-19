using RimWorld;
using Verse.AI;
using Verse;

namespace Expeditions;

// On every comms console (added by DefInjector): the way into the Expeditions window.
public class CompExpeditionsConsole : ThingComp
{
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
