using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Expeditions;

public partial class Window_Expeditions
{
    // With many mods the full catalog is thousands of items: the list stays empty until the search has this many
    // letters, and only shows items from the chosen mod.
    private const int MinSearchLetters = 3;

    private string search = "";
    private ThingDef? selectedItem;
    private ModContentPack? prospectMod = LoadedModManager.RunningModsListForReading.FirstOrDefault(m => m.IsCoreMod);
    private List<ThingDef> filtered = new();
    private (string search, ModContentPack? mod) filteredFor = ("\0", null);

    private List<ThingDef> FilteredItems()
    {
        string s = search.Trim();
        if (filteredFor == (s, prospectMod)) return filtered;
        filteredFor = (s, prospectMod);
        filtered = s.Length < MinSearchLetters
            ? new List<ThingDef>()
            : ProspectCatalog.All
                .Where(d => d.modContentPack == prospectMod && d.label.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
        return filtered;
    }

    // Mods that have at least one item that can be prospected: Core first, then the DLCs, then the rest by name.
    private static List<ModContentPack> ProspectMods() =>
        ProspectCatalog.All.Select(d => d.modContentPack).Where(m => m != null).Distinct()
            .OrderByDescending(m => m.IsCoreMod).ThenByDescending(m => m.IsOfficialMod).ThenBy(m => m.Name)
            .ToList();

    private void ProspectList(Rect rect, ExpeditionsLook look)
    {
        if (look.Button(new Rect(rect.x, rect.y, rect.width, 26f), (prospectMod?.Name ?? "?") + " ▾"))
            Find.WindowStack.Add(new FloatMenu(ProspectMods().Select(m => new FloatMenuOption(m.Name, () =>
            {
                prospectMod = m;
                listScroll = Vector2.zero;
            })).ToList()));

        string next = Widgets.TextField(new Rect(rect.x, rect.y + 30f, rect.width, 26f), search);
        if (next != search)
        {
            search = next;
            listScroll = Vector2.zero;
        }
        Rect list = new(rect.x, rect.y + 60f, rect.width, rect.height - 60f);
        if (search.Trim().Length < MinSearchLetters)
        {
            Label(list.ContractedBy(4f), "Exp_SearchHint".Translate(MinSearchLetters), GameFont.Tiny, look.TextDim);
            return;
        }
        if (FilteredItems().Count == 0)
        {
            Label(list.ContractedBy(4f), "Exp_SearchNoMatch".Translate(prospectMod?.Name ?? "?"), GameFont.Tiny, look.TextDim);
            return;
        }
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
