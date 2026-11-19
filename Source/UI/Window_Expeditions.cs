using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Expeditions;

// The Expeditions window (layout A): tabs on top, a slim list on the left, the colony's own map filling the rest,
// and a details drawer under the map. Opened from a comms console.
public partial class Window_Expeditions : ExpeditionsWindow
{
    public enum Tab
    {
        Opportunities,
        Prospect,
        Active,
        Captured,
    }

    private const float TabHeight = 30f;
    private const float ListWidth = 200f;
    private const float DrawerHeight = 190f;
    private const float Gap = 6f;

    private readonly Thing console;
    private Tab tab;
    private Vector2 listScroll;

    private Map Map => console.Map;
    private PlanetTile Home => console.Map.Tile;
    private static ExpeditionsGame Game => ExpeditionsGame.Instance;

    public override Vector2 InitialSize => new(1100f, 720f);

    protected override string Title => "Exp_WindowTitle".Translate();

    public Window_Expeditions(Thing console)
    {
        this.console = console;
        doCloseX = true;
        draggable = true;
        closeOnClickedOutside = false;
        absorbInputAroundWindow = false;
        preventCameraMotion = false;
    }

    public override void WindowUpdate()
    {
        base.WindowUpdate();
        if (console.Destroyed || !console.Spawned) Close();
    }

    protected override void Contents(Rect inRect, ExpeditionsLook look)
    {
        DrawTabs(new Rect(inRect.x, inRect.y, inRect.width, TabHeight), look);
        Rect body = new(inRect.x, inRect.y + TabHeight + Gap, inRect.width, inRect.height - TabHeight - Gap);
        Rect list = new(body.x, body.y, ListWidth, body.height);
        Rect right = new(list.xMax + Gap, body.y, body.width - ListWidth - Gap, body.height);
        Rect map = new(right.x, right.y, right.width, right.height - DrawerHeight - Gap);
        Rect drawer = new(right.x, map.yMax + Gap, right.width, DrawerHeight);

        look.Section(list);
        look.Section(drawer);
        Rect listInner = list.ContractedBy(4f);
        Rect drawerInner = drawer.ContractedBy(8f);
        switch (tab)
        {
            case Tab.Opportunities:
                OpportunityList(listInner, look);
                OpportunityDrawer(drawerInner, look);
                break;
            case Tab.Prospect:
                ProspectList(listInner, look);
                ProspectDrawer(drawerInner, look);
                break;
            case Tab.Active:
                ActiveList(listInner, look);
                ActiveDrawer(drawerInner, look);
                break;
            case Tab.Captured:
                CapturedList(listInner, look);
                CapturedDrawer(drawerInner, look);
                break;
        }
        DrawMap(map, look);
    }

    private void DrawTabs(Rect rect, ExpeditionsLook look)
    {
        var tabs = new (Tab tab, string label)[]
        {
            (Tab.Opportunities, "Exp_TabOpportunities".Translate()),
            (Tab.Prospect, "Exp_TabProspect".Translate()),
            (Tab.Active, "Exp_TabActive".Translate(Game.active.Count)),
            (Tab.Captured, "Exp_TabCaptured".Translate(CaptiveTracker.All().Count())),
        };
        float w = Math.Min(160f, rect.width / tabs.Length);
        for (int i = 0; i < tabs.Length; i++)
        {
            if (!look.Tab(new Rect(rect.x + i * w, rect.y, w - 4f, rect.height), tabs[i].label, tab == tabs[i].tab)) continue;
            if (tab != tabs[i].tab) listScroll = Vector2.zero;
            tab = tabs[i].tab;
        }
    }

    // A scrolling list of fixed-height rows; only the visible rows are drawn.
    private void RowList<T>(Rect rect, IReadOnlyList<T> items, float rowHeight, Func<T, bool> isSelected, Action<Rect, T> draw, Action<T> select,
        ExpeditionsLook look)
    {
        Rect view = new(0f, 0f, rect.width - 16f, items.Count * rowHeight);
        Widgets.BeginScrollView(rect, ref listScroll, view);
        int first = Mathf.Max(0, (int)(listScroll.y / rowHeight));
        int last = Mathf.Min(items.Count - 1, (int)((listScroll.y + rect.height) / rowHeight) + 1);
        for (int i = first; i <= last; i++)
        {
            Rect row = new(0f, i * rowHeight, view.width, rowHeight);
            look.Row(row, isSelected(items[i]), true, i % 2 == 1);
            draw(row.ContractedBy(4f, 2f), items[i]);
            if (Widgets.ButtonInvisible(row)) select(items[i]);
        }
        Widgets.EndScrollView();
    }

    private static void Label(Rect rect, string text, GameFont font = GameFont.Small, Color? color = null, TextAnchor anchor = TextAnchor.UpperLeft)
    {
        var oldFont = Text.Font;
        var oldAnchor = Text.Anchor;
        Text.Font = font;
        Text.Anchor = anchor;
        if (color != null) GUI.color = color.Value;
        Widgets.Label(rect, text);
        GUI.color = Color.white;
        Text.Font = oldFont;
        Text.Anchor = oldAnchor;
    }

    private static string Days(float days) => days < 0 ? "?" : "Exp_Days".Translate(days.ToString("0.#"));

    private static string Pct(double d) => ((float)d).ToStringPercent();

    private static string Ticks(int ticks) => Math.Max(0, ticks).ToStringTicksToPeriod();
}
