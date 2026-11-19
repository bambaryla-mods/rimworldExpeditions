using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Expeditions;

public partial class Window_Expeditions
{
    private string search = "";
    private ThingDef? selectedItem;
    private List<ThingDef>? filtered;
    private string filteredFor = "\0";

    private List<ThingDef> FilteredItems()
    {
        if (filtered != null && filteredFor == search) return filtered;
        filteredFor = search;
        string s = search.Trim();
        filtered = s.Length == 0
            ? ProspectCatalog.All.ToList()
            : ProspectCatalog.All.Where(d => d.label.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        return filtered;
    }

    private void ProspectList(Rect rect, ExpeditionsLook look)
    {
        string next = Widgets.TextField(new Rect(rect.x, rect.y, rect.width, 26f), search);
        if (next != search)
        {
            search = next;
            listScroll = Vector2.zero;
        }
        Rect list = new(rect.x, rect.y + 30f, rect.width, rect.height - 30f);
        RowList(list, FilteredItems(), 28f, d => d == selectedItem, (r, d) =>
        {
            Widgets.ThingIcon(new Rect(r.x, r.y, 22f, 22f), d);
            bool asked = ProspectService.AskedThisWindow(d);
            Label(new Rect(r.x + 26f, r.y + 2f, r.width - 66f, 22f), d.LabelCap, GameFont.Tiny, asked ? look.TextDim : null);
            Label(new Rect(r.xMax - 40f, r.y + 2f, 40f, 22f), Pct(ProspectCatalog.Chance(d)), GameFont.Tiny, look.TextDim, TextAnchor.UpperRight);
        }, d => selectedItem = d, look);
    }

    private void ProspectDrawer(Rect rect, ExpeditionsLook look)
    {
        Label(new Rect(rect.x, rect.yMax - 20f, rect.width, 20f),
            "Exp_SeedChanges".Translate(Ticks((int)ProspectSeed.TicksUntilNextWindow(Find.TickManager.TicksAbs))), GameFont.Tiny, look.TextDim);
        if (selectedItem == null)
        {
            Label(rect, "Exp_ProspectHelp".Translate(), color: look.TextDim);
            return;
        }
        ThingDef d = selectedItem;
        Widgets.ThingIcon(new Rect(rect.x, rect.y, 48f, 48f), d);
        Label(new Rect(rect.x + 56f, rect.y, rect.width - 56f, 28f), d.LabelCap, GameFont.Medium);
        Label(new Rect(rect.x + 56f, rect.y + 28f, rect.width - 56f, 20f),
            "Exp_LeadChance".Translate(Pct(ProspectCatalog.Chance(d))), GameFont.Tiny, look.TextDim);
        Label(new Rect(rect.x, rect.y + 56f, rect.width * 0.6f, 60f), d.description ?? "", GameFont.Tiny, look.TextDim);

        if (Game.prospectTicksDone.TryGetValue(d, out int done) && done > 0)
            Label(new Rect(rect.x, rect.y + 118f, rect.width, 20f), "Exp_ProspectInProgress".Translate(), GameFont.Tiny);

        bool can = ProspectService.CanStart(d, out string reason);
        var c = console as Building_CommsConsole;
        if (can && (c == null || !c.CanUseCommsNow))
        {
            can = false;
            reason = "Exp_NeedConsole".Translate();
        }
        Rect start = new(rect.xMax - 140f, rect.y + 8f, 140f, 30f);
        if (look.Button(start, "Exp_StartProspect".Translate(), can, LookTone.Confirm) && can && c != null)
            Find.WindowStack.Add(new FloatMenu(ProspectorOptions(c, d)));
        if (!can) Label(new Rect(start.x - 160f, start.yMax + 4f, 300f, 40f), reason, GameFont.Tiny, ColorLibrary.RedReadable, TextAnchor.UpperRight);
    }

    private List<FloatMenuOption> ProspectorOptions(Building_CommsConsole c, ThingDef item)
    {
        var options = Map.mapPawns.FreeColonistsSpawned
            .Where(p => !p.Downed && !p.InMentalState && !Game.IsCommitted(p) && p.CanReach(c, PathEndMode.InteractionCell, Danger.Deadly))
            .Select(p => new FloatMenuOption($"{p.LabelShortCap} ({Ticks(ProspectService.TotalTicks(p))})", () => CompExpeditionsConsole.StartProspect(p, c, item)))
            .ToList();
        if (options.Count == 0) options.Add(new FloatMenuOption("Exp_NobodyFree".Translate(), null));
        return options;
    }
}
