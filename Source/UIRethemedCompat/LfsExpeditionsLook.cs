using System.Runtime.CompilerServices;
using UIRethemed.Core;
using UnityEngine;
using Verse;

namespace Expeditions.UIRethemed;

// [LFS] UI Rethemed (Core) is a UI theme. This assembly only loads when its Core is active (see LoadFolders.xml)
// and draws the Expeditions windows with its widgets: the frame follows the theme's own popups, lists and rows are
// its cards, buttons and tabs its buttons. ExpeditionsLook.Blame drops back to the vanilla look if anything here
// stops working after a theme update.
//
// No UI Rethemed type may sit in a field here: RimWorld lists this assembly's types while loading mods, possibly
// before UI Rethemed itself is loaded (mod order), and a field's type is loaded right then. Method bodies are fine.
[StaticConstructorOnStartup]
public static class UIRethemedCompat
{
    static UIRethemedCompat()
    {
        try
        {
            ExpeditionsLook.Use(new LfsExpeditionsLook());
            Log.Message("[Expeditions] UI Rethemed detected: the Expeditions windows use its theme.");
        }
        catch (Exception e)
        {
            Log.Warning("[Expeditions] Could not use UI Rethemed, the Expeditions windows keep the vanilla look: " + e);
        }
    }
}

public class LfsExpeditionsLook : ExpeditionsLook
{
    private const float TitleHeight = 40f;
    private const float Pad = 10f;

    // When each window was first drawn, for the title accent's grow-in.
    private readonly ConditionalWeakTable<Window, StrongBox<float>> opened = new();

    public override bool Themed => true;

    public override Rect BeginWindow(Window window, Rect inRect, string title)
    {
        float since = opened.GetValue(window, _ => new StrongBox<float>(ThemeMotion.Now)).Value;
        var bar = new Rect(inRect.x, inRect.y, inRect.width, TitleHeight);
        Rounded.BeginBatch();
        try
        {
            Rounded.WindowBackground(inRect);
            Rounded.Fill(bar, Theme.TitleBarFill, Corners.Top);
        }
        finally
        {
            Rounded.EndBatch();
        }
        Faces.FadingHex(bar, Theme.TitleAccent, Theme.TitleBarFill, 1f, 0.6f);
        CoreWidgets.Accent(bar, Theme.TitleAccent, ThemeMotion.On ? ThemeMotion.Now - since : float.MaxValue);
        float right = CoreWidgets.HeaderButtons(bar, () => window.Close());
        CoreWidgets.Line(new Rect(bar.x + 14f, bar.y, Mathf.Max(0f, right - bar.x - 18f), bar.height), title, Color.white, GameFont.Medium);
        return new Rect(inRect.x + Pad, bar.yMax + 8f, inRect.width - Pad * 2f, inRect.yMax - bar.yMax - 8f - Pad);
    }

    public override void EndWindow(Window window, Rect inRect)
    {
        Theme.CloseScrolls();
        Theme.Reset();
        Rounded.Border(inRect, Rounded.WindowBorderColor);
    }

    public override void Section(Rect rect)
    {
        Rounded.BeginBatch();
        try
        {
            Rounded.Fill(rect, Theme.BG);
            Rounded.Border(rect, Theme.BGDark);
        }
        finally
        {
            Rounded.EndBatch();
        }
    }

    public override void Row(Rect rect, bool selected, bool hoverable, bool alternate) =>
        CoreWidgets.Card(rect.ContractedBy(1f), selected, Theme.HighlightGreen, hoverable);

    public override void Divider(float x, float y, float width) => Theme.Divider(x, y, width);

    public override bool Button(Rect rect, string label, bool active = true, LookTone tone = LookTone.Neutral)
    {
        bool confirm = tone == LookTone.Confirm;
        return CoreWidgets.Button(rect, label, accent: confirm ? Theme.HighlightGreen : Theme.HighlightGray,
            enabled: active, raised: confirm && active) && active;
    }

    public override bool Tab(Rect rect, string label, bool selected) =>
        CoreWidgets.Button(rect, label, accent: selected ? Theme.HighlightGreen : Theme.HighlightGray, enabled: true, raised: selected);

    public override Color TextDim => Theme.TextDim;
}
