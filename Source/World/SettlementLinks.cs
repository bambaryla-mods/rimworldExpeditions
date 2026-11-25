using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Expeditions;

public enum SettlementEnd
{
    Raid,    // it stands; the squad hit it and left
    Destroy, // razed, as if a caravan had defeated it
    Nothing, // the squad got nowhere near enough to matter
}

// A site def offered for a real settlement on the world map: whose settlements, how strong, and how it ends.
public class SettlementLinkProps
{
    public Type workerClass = typeof(SettlementLinkWorker);
    public FactionNeed need = FactionNeed.Hostile; // the owner's standing with the colony
    public List<FactionDef> factionDefs = new();   // when set, only settlements of these
    public SettlementEnd onSuccess = SettlementEnd.Raid;
    public SettlementEnd onPartial = SettlementEnd.Raid;
    public float fightPerPoint = 1f;
    public float fightMin = 200f;
    public bool raidCredit = true;    // a Success or Partial counts as a raid for Ideology
    public bool affectRelations;      // a non-hostile owner turns hostile on any rolled outcome, as when attacked
    public RetaliationProps? retaliation; // who answers a partial win (SiteFaction: the settlement's owner)

    [Unsaved] private SettlementLinkWorker? worker;

    public SettlementLinkWorker Worker(ExpeditionSiteDef def)
    {
        if (worker == null)
        {
            worker = (SettlementLinkWorker)Activator.CreateInstance(workerClass)!;
            worker.def = def;
        }
        return worker;
    }

    public IEnumerable<string> ConfigErrors()
    {
        if (workerClass == null || !typeof(SettlementLinkWorker).IsAssignableFrom(workerClass))
            yield return $"settlementLink.workerClass {workerClass} is not a SettlementLinkWorker";
        if (fightMin < 0f || fightPerPoint < 0f) yield return "settlementLink fight numbers must not be negative";
    }
}

// How one kind of settlement offer plays: which settlements qualify, how strong they are, and how they end.
public class SettlementLinkWorker
{
    public ExpeditionSiteDef def = null!;

    protected SettlementLinkProps Props => def.settlementLink!;

    // Whether this settlement can be offered (beyond its owner matching `need` and `factionDefs`).
    public virtual bool CanOffer(Settlement s) => true;

    // Whether the squad should still go: false calls it back, the way a vanished settlement does.
    public virtual bool StillWanted(Opportunity o) => true;

    // Settlements carry no threat points: they are as strong as a site made now.
    public virtual float FightRequirement(Settlement s) =>
        (float)SiteLinkMath.FightRequirement(StorytellerUtility.DefaultSiteThreatPointsNow(), Props.fightPerPoint, Props.fightMin);

    // The whole haul, for a kind of settlement whose stores are known; null rolls the site's table as usual.
    public virtual List<Thing>? MakeLoot(Expedition e, Outcome outcome, bool found) => null;

    public virtual IEnumerable<TaggedString> InfoLines(Opportunity o)
    {
        Settlement s = o.linkedSettlement!;
        yield return "Exp_LinkThreat".Translate(FightRequirement(s).ToString("F0"));
        if (Props.onSuccess == SettlementEnd.Destroy) yield return "Exp_SettlementRazeWarning".Translate(s.LabelCap);
        if (Props.affectRelations && s.Faction is { } f && !f.HostileTo(Faction.OfPlayer))
            yield return "Exp_WorksiteTurnsHostile".Translate(f.Name).Colorize(ColorLibrary.RedReadable);
    }

    // What becomes of the settlement and its owner once the squad is done. Runs outside the seeded Rand state.
    public virtual void Conclude(Settlement s, Outcome outcome, ExpeditionReport report, List<Pawn> raiders)
    {
        if (Props.affectRelations && s.Faction is { } f && !f.IsPlayer && !f.defeated)
        {
            TaggedString text = "";
            SettlementUtility.AffectRelationsOnAttacked(s, ref text);
            if (!text.NullOrEmpty()) report.relationsText = text.Resolve().Trim();
        }
        if (outcome != Outcome.Failure && Props.raidCredit && raiders.Count > 0) IdeoUtility.Notify_PlayerRaidedSomeone(raiders);
        SettlementEnd end = outcome switch
        {
            Outcome.Success => Props.onSuccess,
            Outcome.Partial => Props.onPartial,
            _ => SettlementEnd.Nothing,
        };
        switch (end)
        {
            case SettlementEnd.Destroy:
                report.siteFateText = "Exp_SettlementRazed".Translate(s.LabelCap);
                SettlementLinks.Destroy(s);
                break;
            case SettlementEnd.Raid:
                report.siteFateText = "Exp_SettlementRaided".Translate(s.LabelCap);
                break;
        }
    }
}

public static class SettlementLinks
{
    // Still there, still its owner's kind, nobody in it in person, and still wanted by its worker.
    public static bool Alive(Opportunity o)
    {
        if (o.linkedSettlement is not { } s || o.site.settlementLink is not { } sl) return false;
        if (!s.Spawned || s.Destroyed || s.HasMap) return false;
        Faction? owner = o.enemy ?? o.faction;
        if (s.Faction != owner || !OfferGenerator.Wants(sl.need, s.Faction, sl.factionDefs)) return false;
        return Effects.Safe(o.site, () => sl.Worker(o.site).StillWanted(o), true);
    }

    public static bool Linked(Settlement s)
    {
        var game = ExpeditionsGame.Instance;
        return game.offers.Any(o => o.linkedSettlement == s) || game.active.Any(e => e.opp.linkedSettlement == s);
    }

    // Taken by something an immediate site can't push aside: a squad already out, or another immediate offer.
    public static bool Held(Settlement s)
    {
        var game = ExpeditionsGame.Instance;
        return game.offers.Any(o => o.linkedSettlement == s && o.site.immediate) || game.active.Any(e => e.opp.linkedSettlement == s);
    }

    // The settlement still stands and nobody is in it in person: the squad is being called back, not finding it gone.
    public static bool Recalled(Expedition e) =>
        e.opp.site.settlementLink != null && e.opp.linkedSettlement is { Spawned: true, Destroyed: false, HasMap: false };

    // The settlement went away, or its worker called the squad back, before they were done: they come home.
    public static bool Lost(Expedition e) =>
        e.opp.site.settlementLink != null && e.phase != Phase.Returning && !Alive(e.opp);

    // Settlements in range a site def could be offered for, not already offered or under way.
    public static IEnumerable<Settlement> Candidates(ExpeditionSiteDef site, PlanetTile home)
    {
        SettlementLinkProps sl = site.settlementLink!;
        int range = ExpeditionsMod.Settings.range;
        return Find.WorldObjects.Settlements.Where(s => s.Tile.Layer == home.Layer && s.Spawned && !s.HasMap
            && OfferGenerator.Wants(sl.need, s.Faction, sl.factionDefs) && WorldArea.InRange(home, s.Tile, range)
            && !(site.immediate ? Held(s) : Linked(s))
            && Effects.Safe(site, () => sl.Worker(site).CanOffer(s), false));
    }

    // What vanilla does when a settlement is defeated, without a map: a destroyed settlement in its place, the faction
    // defeated when it was the last one, goodwill from the owner's enemies, and the letter.
    public static void Destroy(Settlement s)
    {
        Faction owner = s.Faction;
        var destroyed = (DestroyedSettlement)WorldObjectMaker.MakeWorldObject(s.Tile.LayerDef.DestroyedSettlementWorldObjectDef);
        destroyed.Tile = s.Tile;
        destroyed.SetFaction(owner);
        Find.WorldObjects.Add(destroyed);
        var sb = new System.Text.StringBuilder();
        sb.Append("LetterFactionBaseDefeatedNoRaids".Translate(s.Label));
        bool last = !Find.WorldObjects.Settlements.Any(x => x != s && x.Faction == owner);
        if (last && owner != null)
        {
            owner.defeated = true;
            sb.AppendLine().AppendLine().Append("LetterFactionBaseDefeated_FactionDestroyed".Translate(owner.Name));
        }
        if (owner != null)
        {
            foreach (Faction f in Find.FactionManager.AllFactions)
            {
                if (f.Hidden || f.IsPlayer || f == owner || !f.HostileTo(owner)) continue;
                FactionRelationKind before = f.PlayerRelationKind;
                Faction.OfPlayer.TryAffectGoodwillWith(f, 20, canSendMessage: false, canSendHostilityLetter: false, HistoryEventDefOf.DestroyedEnemyBase);
                sb.AppendLine().AppendLine().Append("RelationsWith".Translate(f.Name) + ": " + 20.ToStringWithSign());
                f.TryAppendRelationKindChangedInfo(sb, before, f.PlayerRelationKind);
            }
        }
        Find.LetterStack.ReceiveLetter("LetterLabelFactionBaseDefeated".Translate(), sb.ToString(), LetterDefOf.PositiveEvent,
            new GlobalTargetInfo(s.Tile), owner);
        s.Destroy();
    }
}
