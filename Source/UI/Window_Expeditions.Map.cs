using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Expeditions;

public partial class Window_Expeditions
{
    private const float MaxZoom = 4f;
    private const float HitRadius = 10f;

    private float zoom = 1f;
    private Vector2 viewCenter = new(0.5f, 0.5f);

    private static readonly Color Gold = new(0.85f, 0.71f, 0.35f);
    private static readonly Color SquadBlue = new(0.35f, 0.66f, 0.85f);
    private static readonly Color HoloCyan = new(0.5f, 0.9f, 1f);

    private bool Holo => ExpeditionsMod.Settings.style == MapStyle.Holo;

    private void DrawMap(Rect rect, ExpeditionsLook look)
    {
        BakedMap map = MapBaker.Get(Home, ExpeditionsMod.Settings.range, ExpeditionsMod.Settings.style);
        HandleMapInput(rect);

        // The visible part of the square map, letterboxed to fit the rect.
        float side = Mathf.Min(rect.width, rect.height);
        Rect stretched = new(rect.x, rect.y, rect.width, rect.height);
        float spanX = rect.width / side / zoom, spanY = rect.height / side / zoom;
        Rect uv = new(viewCenter.x - spanX / 2f, viewCenter.y - spanY / 2f, spanX, spanY);

        Widgets.DrawBoxSolid(rect, Holo ? new Color(0.024f, 0.071f, 0.094f) : new Color(0.08f, 0.09f, 0.1f));
        GUI.DrawTextureWithTexCoords(stretched, map.texture, new Rect(uv.x, 1f - uv.y - uv.height, uv.width, uv.height));

        Widgets.BeginGroup(rect);
        Vector2 ToScreen(Vector2 p) => new((p.x - uv.x) / uv.width * rect.width, (p.y - uv.y) / uv.height * rect.height);
        Vector2 home = ToScreen(map.ToUV(Home));

        // Range ring: the bake is scaled so the farthest tile sits at 95% of the half size.
        Ring(home, 0.475f / uv.width * rect.width, Holo ? HoloCyan : Color.white, 0.35f);

        foreach (Settlement s in Find.WorldObjects.Settlements)
        {
            if (s.Faction == null || s.Faction.IsPlayer || !WorldArea.InRange(Home, s.Tile, ExpeditionsMod.Settings.range)) continue;
            Vector2 p = ToScreen(map.ToUV(s.Tile));
            Rect box = new(p.x - 3f, p.y - 3f, 6f, 6f);
            Widgets.DrawBoxSolid(box, s.Faction.Color * new Color(1f, 1f, 1f, 0.8f));
            if (Mouse.IsOver(box.ExpandedBy(4f))) TooltipHandler.TipRegion(box.ExpandedBy(4f), $"{s.LabelCap} ({s.Faction.Name})");
        }

        foreach (Expedition e in Game.active)
        {
            Vector2 poi = ToScreen(map.ToUV(e.opp.tile));
            DashedLine(home, poi, e == selectedExp ? Gold : SquadBlue, e == selectedExp ? 2.5f : 1.5f);
            Vector2 at = e.phase switch
            {
                Phase.Outbound => Vector2.Lerp(home, poi, e.PhaseProgress),
                Phase.OnSite => poi,
                Phase.Returning => Vector2.Lerp(poi, home, e.PhaseProgress),
                _ => home,
            };
            Dot(at, 7f, SquadBlue);
            if (Click(at)) { tab = Tab.Active; selectedExp = e; selectedReport = null; }
            Hover(at, $"{e.opp.Label}: {("Exp_Phase_" + e.phase).Translate()}");
        }

        foreach (Opportunity o in VisibleOffers())
        {
            Vector2 p = ToScreen(map.ToUV(o.tile));
            bool selected = o == selectedOpp;
            if (selected) Diamond(p, 16f, Color.white);
            Diamond(p, selected ? 12f : 10f, o.site.color);
            if (Click(p)) Select(o);
            Hover(p, o.Label);
        }

        Diamond(home, 14f, Gold);
        Hover(home, "Exp_Home".Translate());
        Widgets.EndGroup();

        StyleToggle(new Rect(rect.xMax - 150f, rect.y + 6f, 144f, 24f), look);
    }

    private void HandleMapInput(Rect rect)
    {
        Event ev = Event.current;
        if (!Mouse.IsOver(rect)) return;
        if (ev.type == EventType.ScrollWheel)
        {
            zoom = Mathf.Clamp(zoom * (ev.delta.y < 0 ? 1.15f : 1f / 1.15f), 1f, MaxZoom);
            ev.Use();
        }
        else if (ev.type == EventType.MouseDrag && (ev.button == 1 || ev.button == 2))
        {
            float side = Mathf.Min(rect.width, rect.height) * zoom;
            viewCenter -= ev.delta / side;
            ev.Use();
        }
        viewCenter = new Vector2(Mathf.Clamp01(viewCenter.x), Mathf.Clamp01(viewCenter.y));
    }

    private void StyleToggle(Rect rect, ExpeditionsLook look)
    {
        float w = rect.width / 2f;
        foreach (MapStyle style in Enum.GetValues<MapStyle>())
        {
            Rect b = new(rect.x + (int)style * w, rect.y, w - 2f, rect.height);
            if (!look.Tab(b, ("Exp_Style_" + style).Translate(), ExpeditionsMod.Settings.style == style)) continue;
            ExpeditionsMod.Settings.style = style;
            LoadedModManager.GetMod<ExpeditionsMod>().WriteSettings();
        }
    }

    private static bool Click(Vector2 p)
    {
        Event ev = Event.current;
        if (ev.type != EventType.MouseDown || ev.button != 0 || Vector2.Distance(ev.mousePosition, p) > HitRadius) return false;
        ev.Use();
        return true;
    }

    private static void Hover(Vector2 p, string tip)
    {
        Rect r = new(p.x - HitRadius, p.y - HitRadius, HitRadius * 2f, HitRadius * 2f);
        if (Mouse.IsOver(r)) TooltipHandler.TipRegion(r, tip);
    }

    private void Diamond(Vector2 c, float size, Color color)
    {
        Matrix4x4 m = GUI.matrix;
        if (Holo)
        {
            UI.RotateAroundPivot(45f, c);
            GUI.color = color * new Color(1f, 1f, 1f, 0.25f);
            GUI.DrawTexture(new Rect(c.x - size, c.y - size, size * 2f, size * 2f), BaseContent.WhiteTex);
            GUI.matrix = m;
        }
        UI.RotateAroundPivot(45f, c);
        GUI.color = color;
        GUI.DrawTexture(new Rect(c.x - size / 2f, c.y - size / 2f, size, size), BaseContent.WhiteTex);
        GUI.matrix = m;
        GUI.color = Color.white;
    }

    private static void Dot(Vector2 c, float size, Color color)
    {
        GUI.color = color;
        GUI.DrawTexture(new Rect(c.x - size / 2f, c.y - size / 2f, size, size), BaseContent.WhiteTex);
        GUI.color = Color.white;
    }

    private static void DashedLine(Vector2 a, Vector2 b, Color color, float width)
    {
        float len = Vector2.Distance(a, b);
        int dashes = Mathf.Max(1, (int)(len / 8f));
        for (int i = 0; i < dashes; i += 2)
            Widgets.DrawLine(Vector2.Lerp(a, b, i / (float)dashes), Vector2.Lerp(a, b, Mathf.Min(1f, (i + 1) / (float)dashes)), color, width);
    }

    private static void Ring(Vector2 c, float radius, Color color, float alpha)
    {
        const int segments = 72;
        Color col = color * new Color(1f, 1f, 1f, alpha);
        for (int i = 0; i < segments; i += 2)
        {
            float a0 = i / (float)segments * Mathf.PI * 2f, a1 = (i + 1) / (float)segments * Mathf.PI * 2f;
            Widgets.DrawLine(c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * radius, c + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius, col, 1f);
        }
    }
}
