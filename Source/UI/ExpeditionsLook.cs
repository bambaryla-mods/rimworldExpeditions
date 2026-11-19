using System.Diagnostics;
using UnityEngine;
using Verse;

namespace Expeditions;

// What a button does, so a themed look can colour it; the vanilla look ignores it.
public enum LookTone
{
    Neutral,
    Confirm,
}

// The drawing primitives the Expeditions windows are built from. This class is the vanilla look. The optional
// UI Rethemed assembly (Compat/UIRethemed, loaded only with [LFS] UI Rethemed - Core) replaces it with a subclass
// that draws the same primitives with that mod's widgets; layout and logic stay in the windows.
public class ExpeditionsLook
{
    public static readonly ExpeditionsLook Vanilla = new();

    public static ExpeditionsLook Current { get; private set; } = Vanilla;

    public virtual bool Themed => false;

    public static void Use(ExpeditionsLook look) => Current = look;

    // A themed draw failed: back to vanilla for the rest of the session, with one warning. Only failures from the
    // look itself (its assembly, the theme's, or their code no longer matching) count.
    public static void Blame(Exception e)
    {
        if (Current == Vanilla || !FromLook(e)) return;
        Current = Vanilla;
        Log.Warning("[Expeditions] The UI Rethemed look failed, the Expeditions windows use the vanilla look from now on: " + e);
    }

    private static bool FromLook(Exception e)
    {
        if (e is TypeLoadException || e is MissingMemberException || e is TypeInitializationException) return true;
        var lookAssembly = Current.GetType().Assembly;
        foreach (StackFrame frame in new StackTrace(e).GetFrames())
        {
            var assembly = frame.GetMethod()?.DeclaringType?.Assembly;
            if (assembly == lookAssembly || assembly?.GetName().Name == "UIRethemedCore") return true;
        }
        return false;
    }

    // Draws the window's frame and title; returns the rect below the title.
    public virtual Rect BeginWindow(Window window, Rect inRect, string title)
    {
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 35f), title);
        Text.Font = GameFont.Small;
        return new Rect(inRect.x, inRect.y + 40f, inRect.width, inRect.height - 40f);
    }

    public virtual void EndWindow(Window window, Rect inRect)
    {
    }

    public virtual void Section(Rect rect) => Widgets.DrawMenuSection(rect);

    public virtual void Row(Rect rect, bool selected, bool hoverable, bool alternate)
    {
        if (alternate) Widgets.DrawLightHighlight(rect);
        if (selected) Widgets.DrawHighlightSelected(rect);
        else if (hoverable) Widgets.DrawHighlightIfMouseover(rect);
    }

    public virtual void Divider(float x, float y, float width) => Widgets.DrawLineHorizontal(x, y, width);

    public virtual bool Button(Rect rect, string label, bool active = true, LookTone tone = LookTone.Neutral) =>
        Widgets.ButtonText(rect, label, active: active);

    public virtual bool Tab(Rect rect, string label, bool selected)
    {
        if (selected) Widgets.DrawHighlightSelected(rect);
        else Widgets.DrawHighlightIfMouseover(rect);
        var anchor = Text.Anchor;
        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = selected ? new Color(0.85f, 0.71f, 0.35f) : Color.white;
        Widgets.Label(rect, label);
        GUI.color = Color.white;
        Text.Anchor = anchor;
        return Widgets.ButtonInvisible(rect);
    }

    // A score bar: `fill` is the squad's share, `mark` is where the requirement sits (both 0..1).
    public virtual void Bar(Rect rect, float fill, float mark)
    {
        Widgets.DrawBoxSolid(rect, new Color(0.07f, 0.07f, 0.07f));
        Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fill), rect.height), new Color(0.35f, 0.66f, 0.85f));
        float mx = rect.x + rect.width * Mathf.Clamp01(mark);
        Widgets.DrawBoxSolid(new Rect(mx - 1f, rect.y - 2f, 2f, rect.height + 4f), new Color(0.85f, 0.71f, 0.35f));
    }

    public virtual Color TextDim => Color.gray;
}

// Base for the Expeditions windows: a themed look draws its own frame, so the vanilla background, shadow, margin
// and close X are off while it is in use, and come back on the next frame if it is dropped.
public abstract class ExpeditionsWindow : Window
{
    private bool themedFlags;
    private bool vanillaCloseX, vanillaBackground, vanillaShadow;

    protected override float Margin => themedFlags ? 0f : base.Margin;

    protected abstract string Title { get; }

    protected abstract void Contents(Rect inRect, ExpeditionsLook look);

    public override void PreOpen()
    {
        base.PreOpen();
        ApplyLook();
    }

    private void ApplyLook()
    {
        bool themed = ExpeditionsLook.Current.Themed;
        if (themed == themedFlags) return;
        if (themed)
        {
            vanillaCloseX = doCloseX;
            vanillaBackground = doWindowBackground;
            vanillaShadow = drawShadow;
            doCloseX = doWindowBackground = drawShadow = false;
        }
        else
        {
            doCloseX = vanillaCloseX;
            doWindowBackground = vanillaBackground;
            drawShadow = vanillaShadow;
        }
        themedFlags = themed;
    }

    public override void DoWindowContents(Rect inRect)
    {
        ApplyLook();
        ExpeditionsLook look = ExpeditionsLook.Current;
        try
        {
            Contents(look.BeginWindow(this, inRect, Title), look);
        }
        catch (Exception e)
        {
            ExpeditionsLook.Blame(e);
            throw;
        }
        finally
        {
            try
            {
                look.EndWindow(this, inRect);
            }
            catch (Exception e)
            {
                ExpeditionsLook.Blame(e);
            }
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }
    }
}
