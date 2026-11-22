using RimWorld;
using UnityEngine;
using Verse;

namespace Expeditions;

// The full report of one expedition: outcome, each pawn's fate, what came home and what was left behind.
public class Window_ExpeditionReport : ExpeditionsWindow
{
    private readonly ExpeditionReport report;
    private Vector2 scroll;

    public override Vector2 InitialSize => new(560f, 620f);

    protected override string Title => report.siteLabel;

    public Window_ExpeditionReport(ExpeditionReport report)
    {
        this.report = report;
        doCloseX = true;
        draggable = true;
        absorbInputAroundWindow = false;
        closeOnClickedOutside = true;
    }

    protected override void Contents(Rect inRect, ExpeditionsLook look)
    {
        Color banner = report.outcome switch
        {
            Outcome.Success => new Color(0.5f, 0.69f, 0.41f),
            Outcome.Partial => new Color(0.85f, 0.6f, 0.23f),
            Outcome.Gone or Outcome.CutShort => new Color(0.55f, 0.55f, 0.55f),
            _ => new Color(0.78f, 0.33f, 0.24f),
        };
        Rect top = new(inRect.x, inRect.y, inRect.width, 44f);
        Widgets.DrawBoxSolid(top, banner * new Color(1f, 1f, 1f, 0.35f));
        Text.Font = GameFont.Medium;
        Widgets.Label(top.ContractedBy(8f, 4f), ("Exp_Outcome_" + report.outcome).Translate());
        Text.Font = GameFont.Small;
        float y = top.yMax + 6f;
        if (report.outcome != Outcome.Failure && report.hasFinds)
        {
            Widgets.Label(new Rect(inRect.x, y, inRect.width, 22f), (report.found ? "Exp_Found" : "Exp_NotFound").Translate());
            y += 26f;
        }
        if (!report.successText.NullOrEmpty())
        {
            Widgets.Label(new Rect(inRect.x, y, inRect.width, 22f), report.successText);
            y += 26f;
        }
        foreach (string? line in new[] { report.relationsText, report.retaliationText, report.siteFateText })
        {
            if (line.NullOrEmpty()) continue;
            float h = Text.CalcHeight(line, inRect.width);
            Widgets.Label(new Rect(inRect.x, y, inRect.width, h), line);
            y += h + 4f;
        }

        Rect list = new(inRect.x, y, inRect.width, inRect.yMax - y);
        var goodwill = Arrival.GoodwillLines(report).ToList();
        float height = report.pawns.Count * 64f + 90f + (report.loot.Count + report.lostToHaul.Count + goodwill.Count) * 24f;
        Rect view = new(0f, 0f, list.width - 16f, height);
        Widgets.BeginScrollView(list, ref scroll, view);
        float row = 0f;
        foreach (PawnResult p in report.pawns)
        {
            Rect r = new(0f, row, view.width, 60f);
            look.Row(r, false, false, (int)(row / 64f) % 2 == 1);
            Widgets.ThingIcon(new Rect(r.x + 4f, r.y + 6f, 48f, 48f), p.pawn);
            Widgets.Label(new Rect(r.x + 60f, r.y + 4f, r.width - 60f, 22f), $"{p.pawn.LabelShortCap}: {("Exp_Fate_" + p.fate).Translate()}");
            string detail = string.Join(", ", p.injuries);
            if (p.xp.Count > 0) detail += (detail.Length > 0 ? " · " : "") + string.Join(", ", p.xp.Select(kv => $"{kv.Key.LabelCap} +{kv.Value:F0} xp"));
            GUI.color = look.TextDim;
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(r.x + 60f, r.y + 26f, r.width - 60f, 32f), detail);
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            row += 64f;
        }
        row += 8f;
        Widgets.Label(new Rect(0f, row, view.width, 22f), "Exp_LootHeader".Translate());
        row += 24f;
        foreach (ThingDefCountClass l in report.loot)
        {
            Widgets.ThingIcon(new Rect(0f, row, 22f, 22f), l.thingDef);
            Widgets.Label(new Rect(26f, row, view.width - 26f, 22f), l.Label);
            row += 24f;
        }
        if (report.lostToHaul.Count > 0)
        {
            Widgets.Label(new Rect(0f, row, view.width, 22f), "Exp_LostToHaulHeader".Translate());
            row += 24f;
            GUI.color = look.TextDim;
            foreach (ThingDefCountClass l in report.lostToHaul)
            {
                Widgets.ThingIcon(new Rect(0f, row, 22f, 22f), l.thingDef);
                Widgets.Label(new Rect(26f, row, view.width - 26f, 22f), l.Label);
                row += 24f;
            }
            GUI.color = Color.white;
        }
        if (goodwill.Count > 0)
        {
            row += 8f;
            foreach (string line in goodwill)
            {
                Widgets.Label(new Rect(0f, row, view.width, 22f), line);
                row += 24f;
            }
        }
        Widgets.EndScrollView();
    }
}

// The return letter, with a button that opens the full report. The report is found in history by its tick.
public class ChoiceLetter_ExpeditionReport : ChoiceLetter
{
    public int reportTick;

    public override IEnumerable<DiaOption> Choices
    {
        get
        {
            ExpeditionReport? report = ExpeditionsGame.Instance.history.FirstOrDefault(r => r.tick == reportTick);
            if (report != null)
                yield return new DiaOption("Exp_OpenReport".Translate())
                {
                    action = () => Find.WindowStack.Add(new Window_ExpeditionReport(report)),
                    resolveTree = true,
                };
            yield return Option_Close;
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref reportTick, "reportTick");
    }
}
