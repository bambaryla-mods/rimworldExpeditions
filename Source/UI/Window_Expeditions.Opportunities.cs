using RimWorld;
using UnityEngine;
using Verse;

namespace Expeditions;

public partial class Window_Expeditions
{
    private const float EvalRefreshFrames = 90;

    private Opportunity? selectedOpp;
    // Each opportunity keeps the squad picked for it until it launches, so a pawn is in at most one of them.
    private readonly Dictionary<Opportunity, List<Pawn>> drafts = new();
    private List<Pawn> squad = new();
    private Ride ride = Ride.Foot;

    // Cached for the selected opportunity, squad and ride; rebuilt on change and every ~1.5 s.
    private string evalKey = "";
    private int evalFrame = -9999;
    private SquadEvaluation? eval;
    private List<RideOption> rides = new();
    private AcceptanceReport launchReport;
    private double tripDays;
    private float foodNeed, foodHave;
    private int medsHave;

    private List<Opportunity> VisibleOffers() =>
        Game.offers.Where(o => WorldArea.InRange(Home, o.tile, ExpeditionsMod.Settings.range))
            .OrderByDescending(o => o.captive != null).ThenByDescending(o => o.prospected).ThenBy(o => o.expiresTick)
            .ToList();

    public void Select(Opportunity opp)
    {
        tab = Tab.Opportunities;
        if (selectedOpp == opp) return;
        selectedOpp = opp;
        if (!drafts.TryGetValue(opp, out List<Pawn>? draft)) drafts[opp] = draft = new List<Pawn>();
        squad = draft;
        ride = Ride.Foot;
        evalKey = "";
    }

    private void OpportunityList(Rect rect, ExpeditionsLook look)
    {
        var offers = VisibleOffers();
        if (selectedOpp != null && !offers.Contains(selectedOpp))
        {
            drafts.Remove(selectedOpp);
            selectedOpp = null;
            squad = new List<Pawn>();
        }
        if (offers.Count == 0)
        {
            Label(rect, "Exp_NoOffers".Translate(), color: look.TextDim);
            return;
        }
        RowList(rect, offers, 46f, o => o == selectedOpp, (r, o) =>
        {
            Label(new Rect(r.x, r.y, r.width, 22f), o.Label);
            string expires = o.expiresTick < 0 ? "" : " · " + "Exp_Expires".Translate(Ticks(o.expiresTick - Find.TickManager.TicksGame));
            Label(new Rect(r.x, r.y + 20f, r.width, 20f), Days(TravelEstimator.NominalFootDays(Home, o.tile)) + expires, GameFont.Tiny, look.TextDim);
        }, Select, look);
    }

    private void RefreshEvaluation()
    {
        if (selectedOpp == null) return;
        string key = $"{selectedOpp.id}|{string.Join(",", squad.Select(p => p.thingIDNumber))}|{ride}";
        if (key == evalKey && Time.frameCount - evalFrame < EvalRefreshFrames) return;
        evalKey = key;
        evalFrame = Time.frameCount;
        squad.RemoveAll(p => p.Destroyed || !p.Spawned || p.Map != Map || Game.IsCommitted(p));
        rides = RideFinder.For(Map, squad, Departure.Tiles(Map, selectedOpp));
        RideOption option = rides.FirstOrDefault(r => r.ride == ride) ?? rides[0];
        eval = SquadEvaluator.Evaluate(squad, selectedOpp, ride, option);
        tripDays = squad.Count == 0 ? -1 : Departure.TripDays(Map, selectedOpp, squad, ride);
        // With no squad picked yet the trip's food is unknown, but the alms are not.
        foodNeed = tripDays < 0 ? selectedOpp.site.supplies?.nutrition ?? 0f : SupplyPlanner.FoodNeed(squad, tripDays, selectedOpp.site);
        foodHave = FoodPlanner.Available(squad, Map, foodNeed, out _);
        medsHave = selectedOpp.site.supplies is { medicine: > 0 } sup ? SupplyPlanner.Medicine(squad, Map, sup.medicine, out _) : 0;
        launchReport = squad.Count == 0 ? "Exp_PickSquad".Translate() : Departure.CanLaunch(selectedOpp, squad, option, Map);
    }

    private void OpportunityDrawer(Rect rect, ExpeditionsLook look)
    {
        if (selectedOpp == null)
        {
            Label(rect, "Exp_SelectOpportunity".Translate(), color: look.TextDim);
            return;
        }
        RefreshEvaluation();
        Opportunity o = selectedOpp;
        float colW = (rect.width - 20f) / 3f;
        Rect info = new(rect.x, rect.y, colW, rect.height);
        Rect bars = new(info.xMax + 10f, rect.y, colW, rect.height);
        Rect team = new(bars.xMax + 10f, rect.y, colW, rect.height);

        // Column 1: what and where, and the three numbers.
        Label(new Rect(info.x, info.y, info.width, 26f), o.Label, GameFont.Medium);
        var tile = Find.WorldGrid[o.tile];
        string where = $"{tile.PrimaryBiome.LabelCap} · {Days(TravelEstimator.NominalFootDays(Home, o.tile))}";
        if (o.faction != null && !o.Label.Contains(o.faction.Name)) where += " · " + o.faction.Name;
        Label(new Rect(info.x, info.y + 28f, info.width, 20f), where, GameFont.Tiny, look.TextDim);
        Label(new Rect(info.x, info.y + 46f, info.width, 40f), o.site.description, GameFont.Tiny, look.TextDim);
        float infoY = info.y + 92f;
        IEnumerable<TaggedString>? lines = o.linkedSite != null && SiteLinks.Alive(o.linkedSite) && o.site.LinkWorker is { } worker
            ? worker.InfoLines(o)
            : o.linkedSettlement != null && SettlementLinks.Alive(o) && o.site.SettlementWorker is { } sworker
                ? Effects.Safe(o.site, () => sworker.InfoLines(o).ToList(), new List<TaggedString>()) : null;
        if (lines != null)
        {
            // What the site's worker says about it: guards, the best haul, warnings.
            foreach (TaggedString line in lines)
            {
                Text.Font = GameFont.Tiny;
                float h = Math.Max(20f, Text.CalcHeight(line, info.width));
                Text.Font = GameFont.Small;
                Label(new Rect(info.x, infoY - 4f, info.width, h), line, GameFont.Tiny);
                infoY += h - 2f;
            }
            infoY += 4f;
        }
        if (eval != null)
        {
            Label(new Rect(info.x, infoY, info.width, 24f),
                "Exp_Numbers".Translate(Pct(eval.success), Pct(eval.find), Days((float)tripDays)));
        }
        GUI.color = foodHave + 0.001f < foodNeed ? ColorLibrary.RedReadable : Color.white;
        Label(new Rect(info.x, infoY + 24f, info.width, 20f), "Exp_Food".Translate(foodNeed.ToString("F1"), foodHave.ToString("F1")), GameFont.Tiny);
        GUI.color = Color.white;
        if (o.site.supplies is { } supplies)
        {
            // The food line above already counts the food to give away; this says how much of it is for them.
            GUI.color = medsHave < supplies.medicine ? ColorLibrary.RedReadable : Color.white;
            Label(new Rect(info.x, infoY + 42f, info.width, 20f),
                "Exp_Supplies".Translate(supplies.nutrition.ToString("F1"), supplies.medicine, medsHave), GameFont.Tiny);
            GUI.color = Color.white;
        }

        // Column 2: one bar per challenge; hover lists who and what gave the score.
        if (eval != null)
        {
            float y = bars.y;
            foreach (ChallengeResult r in eval.challenges)
            {
                Rect row = new(bars.x, y, bars.width, 22f);
                Label(new Rect(row.x, row.y, 70f, row.height), r.site.challenge.LabelCap, GameFont.Tiny);
                look.Bar(new Rect(row.x + 72f, row.y + 7f, row.width - 112f, 8f), (float)(r.score / (r.requirement * 2)), 0.5f);
                Label(new Rect(row.xMax - 36f, row.y, 36f, row.height), r.score.ToString("F0"), GameFont.Tiny, anchor: TextAnchor.UpperRight);
                if (Mouse.IsOver(row)) TooltipHandler.TipRegion(row, ChallengeTip(r));
                y += 24f;
            }
        }

        // Column 3: squad, ride, launch.
        SquadSlots(new Rect(team.x, team.y, team.width, 44f), o);
        RideButtons(new Rect(team.x, team.y + 50f, team.width, 26f), look);
        Rect launch = new(team.xMax - 120f, team.yMax - 32f, 120f, 30f);
        if (look.Button(launch, "Exp_Launch".Translate(), launchReport.Accepted, LookTone.Confirm) && launchReport.Accepted)
        {
            RideOption option = rides.First(r => r.ride == ride);
            Departure.Launch(o, squad.ToList(), option, Map);
            drafts.Remove(o);
            selectedOpp = null;
            squad = new List<Pawn>();
            tab = Tab.Active;
        }
        if (!launchReport.Accepted && !launchReport.Reason.NullOrEmpty())
        {
            Label(new Rect(team.x, team.y + 82f, team.width, team.height - 120f), launchReport.Reason, GameFont.Tiny, ColorLibrary.RedReadable);
            TooltipHandler.TipRegion(launch, launchReport.Reason);
        }
    }

    private static string ChallengeTip(ChallengeResult r)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{r.site.challenge.LabelCap}: {r.score:F0} / {r.requirement:F0} → {Pct(r.chance)}");
        sb.AppendLine(r.site.challenge.description);
        foreach (Contribution c in r.parts) sb.AppendLine().AppendLine(c.explanation);
        return sb.ToString().TrimEnd();
    }

    private void SquadSlots(Rect rect, Opportunity o)
    {
        int slots = Math.Min(o.site.squadSize.max, 6) + squad.Count(p => !p.RaceProps.Humanlike);
        float size = Math.Min(40f, rect.width / Math.Max(1, slots + 1) - 4f);
        for (int i = 0; i < squad.Count; i++)
        {
            Rect slot = new(rect.x + i * (size + 4f), rect.y, size, size);
            Widgets.DrawBoxSolid(slot, new Color(0.15f, 0.15f, 0.15f));
            Widgets.ThingIcon(slot, squad[i]);
            TooltipHandler.TipRegion(slot, squad[i].LabelShortCap + "\n" + "Exp_ClickToRemove".Translate());
            if (Widgets.ButtonInvisible(slot))
            {
                squad.RemoveAt(i);
                evalKey = "";
                return;
            }
        }
        Rect add = new(rect.x + squad.Count * (size + 4f), rect.y, size, size);
        if (Widgets.ButtonText(add, "+")) Find.WindowStack.Add(new FloatMenu(AddOptions(o)));
    }

    private List<FloatMenuOption> AddOptions(Opportunity o)
    {
        // Each pawn's score on the site's heaviest challenge, so the picker shows who fits.
        SiteChallenge? main = o.site.challenges.OrderByDescending(c => c.weight).FirstOrDefault();
        var options = new List<FloatMenuOption>();
        var candidates = Map.mapPawns.FreeColonistsSpawned
            .Concat(Map.mapPawns.SpawnedColonyAnimals.Where(a => a.RaceProps.packAnimal))
            .Where(p => !squad.Contains(p) && !p.Downed && !Game.IsCommitted(p) && Eligibility.Listed(p));
        foreach (Pawn p in candidates)
        {
            string label = p.LabelShortCap;
            Opportunity? other = drafts.FirstOrDefault(d => d.Key != o && d.Value.Contains(p) && Game.offers.Contains(d.Key)).Key;
            if (other != null)
            {
                options.Add(new FloatMenuOption($"{label} ({"Exp_InOtherSquad".Translate(other.Label)})", null));
                continue;
            }
            AcceptanceReport can = Eligibility.CanGo(p, tripDays >= 0 ? tripDays : Departure.TripDays(Map, o, new List<Pawn> { p }, ride));
            if (!can.Accepted)
            {
                options.Add(new FloatMenuOption($"{label} ({can.Reason})", null));
                continue;
            }
            if (main != null && p.RaceProps.Humanlike)
            {
                var e = SquadEvaluator.Evaluate(new[] { p }, o, ride, null);
                label += $" ({main.challenge.label} {e.challenges.First(c => c.site == main).score:F0})";
            }
            else if (!p.RaceProps.Humanlike) label += " (" + "Exp_PackAnimal".Translate() + ")";
            options.Add(new FloatMenuOption(label, () =>
            {
                squad.Add(p);
                evalKey = "";
            }));
        }
        if (options.Count == 0) options.Add(new FloatMenuOption("Exp_NobodyFree".Translate(), null));
        return options;
    }

    private void RideButtons(Rect rect, ExpeditionsLook look)
    {
        float w = rect.width / Math.Max(1, rides.Count);
        for (int i = 0; i < rides.Count; i++)
        {
            RideOption r = rides[i];
            Rect b = new(rect.x + i * w, rect.y, w - 4f, rect.height);
            string label = ("Exp_Ride_" + r.ride).Translate();
            if (r.ride == ride) Widgets.DrawBoxSolid(b.ExpandedBy(2f), new Color(0.85f, 0.71f, 0.35f, 0.6f));
            if (look.Button(b, label, r.available) && r.available)
            {
                ride = r.ride;
                evalKey = "";
            }
            TooltipHandler.TipRegion(b, r.available ? ("Exp_RideTip_" + r.ride).Translate() : (TaggedString)(r.reason ?? ""));
        }
    }
}
