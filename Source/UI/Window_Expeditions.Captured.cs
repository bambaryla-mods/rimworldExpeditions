using UnityEngine;
using Verse;

namespace Expeditions;

public partial class Window_Expeditions
{
    private Pawn? selectedCaptive;

    private void CapturedList(Rect rect, ExpeditionsLook look)
    {
        var captives = CaptiveTracker.All().ToList();
        if (captives.Count == 0)
        {
            Label(rect, "Exp_NoCaptives".Translate(), color: look.TextDim);
            return;
        }
        RowList(rect, captives, 46f, c => c.pawn == selectedCaptive, (r, c) =>
        {
            Widgets.ThingIcon(new Rect(r.x, r.y, 36f, 36f), c.pawn);
            Label(new Rect(r.x + 40f, r.y, r.width - 40f, 22f), c.pawn.LabelShortCap);
            Label(new Rect(r.x + 40f, r.y + 20f, r.width - 40f, 20f), c.captor.Name, GameFont.Tiny, look.TextDim);
        }, c => selectedCaptive = c.pawn, look);
    }

    private void CapturedDrawer(Rect rect, ExpeditionsLook look)
    {
        CaptiveInfo? c = selectedCaptive == null ? null : CaptiveTracker.All().FirstOrDefault(x => x.pawn == selectedCaptive);
        if (c == null)
        {
            Label(rect, "Exp_CapturedHelp".Translate(), color: look.TextDim);
            return;
        }
        Widgets.ThingIcon(new Rect(rect.x, rect.y, 64f, 64f), c.pawn);
        Label(new Rect(rect.x + 72f, rect.y, rect.width - 72f, 28f), c.pawn.LabelShortCap, GameFont.Medium);
        Label(new Rect(rect.x + 72f, rect.y + 30f, rect.width - 72f, 22f), "Exp_HeldBy".Translate(c.captor.Name, Ticks(c.heldTicks)), GameFont.Small, look.TextDim);

        bool planned = CaptiveTracker.HasRescue(c.pawn);
        Rect rescue = new(rect.xMax - 140f, rect.y + 8f, 140f, 30f);
        if (look.Button(rescue, planned ? "Exp_RescuePlanned".Translate() : "Exp_Rescue".Translate(), !planned, LookTone.Confirm) && !planned)
        {
            Opportunity? opp = CaptiveTracker.CreateRescue(c, Home);
            if (opp != null) Select(opp);
            else Messages.Message("Exp_NoRescueSite".Translate(), RimWorld.MessageTypeDefOf.RejectInput, false);
        }
    }
}
