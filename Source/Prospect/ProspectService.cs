using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Expeditions;

// Asking around on the comms console for a specific item. The answer is fixed by the seed window.
public static class ProspectService
{
    private const float TargetValue = 800f;

    public static int CurrentWindow => ProspectSeed.WindowAt(Find.TickManager.TicksAbs);

    public static void RefreshWindow()
    {
        var game = ExpeditionsGame.Instance;
        int w = CurrentWindow;
        if (w == game.prospectWindow) return;
        game.prospectWindow = w;
        game.prospectedThisWindow.Clear();
    }

    public static bool AskedThisWindow(ThingDef def)
    {
        RefreshWindow();
        return ExpeditionsGame.Instance.prospectedThisWindow.Contains(def);
    }

    public static bool CanStart(ThingDef def, out string reason)
    {
        reason = "";
        if (!AskedThisWindow(def)) return true;
        reason = "Exp_NoNewInfo".Translate();
        return false;
    }

    // Speed only: a good talker finishes sooner but does not get better odds.
    public static int TotalTicks(Pawn pawn)
    {
        StatDef stat = pawn.WorkTagIsDisabled(WorkTags.Social) ? StatDefOf.ResearchSpeed : StatDefOf.NegotiationAbility;
        return Math.Max(1, (int)(ExpeditionsDefOf.Tuning.prospectBaseTicks / Math.Max(0.2f, pawn.GetStatValue(stat))));
    }

    public static float Progress(ThingDef def, Pawn pawn) =>
        ExpeditionsGame.Instance.prospectTicksDone.TryGetValue(def, out int done) ? done / (float)TotalTicks(pawn) : 0f;

    public static void Complete(ThingDef def, Map consoleMap)
    {
        var game = ExpeditionsGame.Instance;
        RefreshWindow();
        game.prospectedThisWindow.Add(def);
        game.prospectTicksDone.Remove(def);

        bool lead = ProspectSeed.HasLead(Find.World.info.Seed, game.prospectWindow, def.defName, ProspectCatalog.Chance(def));
        Opportunity? opp = lead ? MakeLead(def, consoleMap.Tile) : null;
        if (opp == null)
        {
            Find.LetterStack.ReceiveLetter("Exp_NoLeadLabel".Translate(), "Exp_NoLeadText".Translate(def.label), LetterDefOf.NeutralEvent);
            return;
        }
        game.offers.Add(opp);
        Find.LetterStack.ReceiveLetter("Exp_LeadFoundLabel".Translate(def.LabelCap),
            "Exp_LeadFoundText".Translate(def.label, opp.site.label), LetterDefOf.PositiveEvent);
    }

    private static Opportunity? MakeLead(ThingDef def, PlanetTile home)
    {
        var cats = ProspectCatalog.CategoriesOf(def).ToHashSet();
        var sites = DefDatabase<ExpeditionSiteDef>.AllDefsListForReading
            .Where(s => !s.FromWorldOnly && s.servesCategories.Any(cats.Contains))
            .InRandomOrder()
            .Append(ExpeditionsDefOf.Exp_Rumor);
        foreach (ExpeditionSiteDef site in sites)
        {
            Opportunity? opp = OfferGenerator.TryMake(site, home, def);
            if (opp == null) continue;
            opp.targetCount = Math.Max(1, (int)Math.Round(TargetValue / def.BaseMarketValue));
            return opp;
        }
        return null;
    }
}
