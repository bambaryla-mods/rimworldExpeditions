using UnityEngine;
using Verse;

namespace Expeditions;

public partial class Window_Expeditions
{
    private Expedition? selectedExp;
    private ExpeditionReport? selectedReport;
    private Vector2 reportScroll;

    // Active expeditions first, then the history, in one list.
    private void ActiveList(Rect rect, ExpeditionsLook look)
    {
        var rows = Game.active.Cast<object>().Concat(Game.history).ToList();
        if (selectedExp != null && !Game.active.Contains(selectedExp)) selectedExp = null;
        if (rows.Count == 0)
        {
            Label(rect, "Exp_NoActive".Translate(), color: look.TextDim);
            return;
        }
        RowList(rect, rows, 46f, o => o == selectedExp || o == selectedReport, (r, o) =>
        {
            if (o is Expedition e)
            {
                Label(new Rect(r.x, r.y, r.width, 22f), e.opp.Label);
                Label(new Rect(r.x, r.y + 20f, r.width, 20f), $"{("Exp_Phase_" + e.phase).Translate()} · {e.PhaseProgress.ToStringPercent()}", GameFont.Tiny, look.TextDim);
            }
            else if (o is ExpeditionReport rep)
            {
                Label(new Rect(r.x, r.y, r.width, 22f), rep.siteLabel, color: look.TextDim);
                Label(new Rect(r.x, r.y + 20f, r.width, 20f), ("Exp_Outcome_" + rep.outcome).Translate(), GameFont.Tiny, look.TextDim);
            }
        }, o =>
        {
            selectedExp = o as Expedition;
            selectedReport = o as ExpeditionReport;
            reportScroll = Vector2.zero;
        }, look);
    }

    private void ActiveDrawer(Rect rect, ExpeditionsLook look)
    {
        if (selectedExp is { } e)
        {
            Label(new Rect(rect.x, rect.y, rect.width, 26f), e.opp.Label, GameFont.Medium);
            Label(new Rect(rect.x, rect.y + 30f, rect.width, 22f),
                "Exp_PhaseLine".Translate(("Exp_Phase_" + e.phase).Translate(), ("Exp_Ride_" + e.ride).Translate(), Ticks(e.phaseEndTick - Find.TickManager.TicksGame)));
            look.Bar(new Rect(rect.x, rect.y + 56f, rect.width * 0.5f, 10f), e.PhaseProgress, 1f);
            if (e.phase == Phase.Departing && look.Button(new Rect(rect.xMax - 130f, rect.y, 130f, 28f), "Exp_CallOff".Translate()))
            {
                Departure.CallOff(e);
                selectedExp = null;
                return;
            }
            if (e.phase == Phase.Outbound)
            {
                Rect recall = new(rect.xMax - 130f, rect.y, 130f, 28f);
                if (e.ride == Ride.DropPods) TooltipHandler.TipRegion(recall, "Exp_RecallNoPods".Translate());
                if (look.Button(recall, "Exp_Recall".Translate(), active: Departure.CanRecall(e)) && Departure.CanRecall(e))
                {
                    Departure.Recall(e);
                    return;
                }
            }
            var people = e.phase == Phase.Departing ? e.squad : e.AwayPawns.Where(p => !RideFinder.VehicleCompat.IsVehicle(p)).ToList();
            for (int i = 0; i < people.Count; i++)
            {
                Rect slot = new(rect.x + i * 44f, rect.y + 76f, 40f, 40f);
                Widgets.ThingIcon(slot, people[i]);
                TooltipHandler.TipRegion(slot, people[i].LabelShortCap);
            }
            return;
        }
        if (selectedReport is { } r)
        {
            Rect open = new(rect.xMax - 130f, rect.y, 130f, 28f);
            if (look.Button(open, "Exp_OpenReport".Translate())) Find.WindowStack.Add(new Window_ExpeditionReport(r));
            Rect text = new(rect.x, rect.y, rect.width - 140f, rect.height);
            string body = Arrival.ReportText(r);
            Rect view = new(0f, 0f, text.width - 16f, Text.CalcHeight(body, text.width - 16f));
            Widgets.BeginScrollView(text, ref reportScroll, view);
            Widgets.Label(view, body);
            Widgets.EndScrollView();
            return;
        }
        Label(rect, "Exp_SelectExpedition".Translate(), color: look.TextDim);
    }
}
