using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Expeditions;

// What happens at the site, rolled off-screen once the squad has spent its time there.
public static class Resolver
{
    // Gear wear by fate, as a fraction of max hit points.
    private const float WearUnharmed = 0.05f;
    private const float WearInjured = 0.2f;

    // Dev mode: force the next outcome.
    public static Outcome? forcedOutcome;

    public static void Resolve(Expedition e)
    {
        var game = ExpeditionsGame.Instance;
        ExpeditionSiteDef site = e.opp.site;
        var tuning = ExpeditionsDefOf.Tuning;
        var report = new ExpeditionReport { siteLabel = e.opp.Label, tick = Find.TickManager.TicksGame };
        e.TakePending(report);
        e.report = report; // so what happens from here on (slaves fleeing) is noted straight into it
        Outcome outcome;

        Rand.PushState(e.opp.Seed);
        try
        {
            var pawns = e.AwayPawns.Where(p => !RideFinder.VehicleCompat.IsVehicle(p)).ToList();
            SquadEvaluation eval = SquadEvaluator.Evaluate(pawns, e.opp, e.ride, RideFinder.Held(e));
            outcome = forcedOutcome ?? OutcomeRoll.Roll(eval.success, Rand.Value, tuning.partialBand);
            forcedOutcome = null;
            bool found = outcome != Outcome.Failure && Rand.Value < eval.find;
            report.outcome = outcome;
            report.found = found;
            report.hasFinds = site.HasFinds || e.opp.target != null || e.opp.captive != null;
            if (outcome == Outcome.Success) report.successText = site.successText;

            foreach (Pawn p in pawns) report.pawns.Add(ApplyFate(e, p, outcome, report));
            PawnEffects(e, outcome, report);
            Effects.Safe(site, () => GiveSupplies(e, outcome, report));
            // Slaves with nobody left to keep them in line all run; the rest may still try.
            SquadEvents.CheckOverlord(e);
            SquadEvents.SlaveRoll(e);

            if (found && outcome == Outcome.Success && e.opp.captive is { } captive) Rescue(e, captive, report);
            foreach (Thing t in LootRoller.Roll(e, outcome, found, report)) e.contents.TryAdd(t);
            if (found && outcome != Outcome.Failure) Recruits(e, outcome, report);
            if (found && outcome != Outcome.Failure) Effects.Safe(site, () => TakeCaptives(e, outcome, report));
            Reward(e, outcome, report);
        }
        finally
        {
            Rand.PopState();
        }

        foreach (OutcomeEffect oe in OutcomeEffectsFor(e).Where(x => x.outcomes.Contains(outcome)))
            Effects.Safe(site, () => oe.Apply(e, outcome, report));

        // A linked settlement ends as its worker says: raided, razed or untouched, and its owner may answer.
        if (site.SettlementWorker is { } sw && e.opp.linkedSettlement is { Spawned: true, Destroyed: false } settlement)
        {
            var raiders = e.AwayPawns.Where(p => p.RaceProps.Humanlike && !p.Dead && !e.IsCaptive(p)).ToList();
            if (outcome == Outcome.Partial && raiders.Count > 0 && Retaliation.For(e) is { } raid)
            {
                e.retaliation = raid;
                report.retaliationText = "Exp_RetaliationLine".Translate(Retaliation.Who(raid));
            }
            Effects.Safe(site, () => sw.Conclude(settlement, outcome, report, raiders));
            if (settlement.Destroyed) e.opp.linkedSettlement = null;
        }

        // A linked vanilla site ends as its worker says, outside the seeded state: these are vanilla calls.
        if (site.LinkWorker is { } worker)
        {
            // A partial win is answered: who will come is read now, before the site is gone.
            if (outcome == Outcome.Partial && e.AwayPawns.Any(p => p.RaceProps.Humanlike && !p.Dead && !e.IsCaptive(p)) && Retaliation.For(e) is { } raid)
            {
                e.retaliation = raid;
                report.retaliationText = "Exp_RetaliationLine".Translate(Retaliation.Who(raid));
            }
            if (e.opp.linkedSite is { } ls && SiteLinks.Alive(ls))
                worker.Conclude(ls, SiteLinks.QuestOf(ls), outcome, report, e.AwayPawns.Where(p => p.RaceProps.Humanlike && !p.Dead && !e.IsCaptive(p)).ToList());
            // A site that is gone is unlinked. One still standing (a harmful site after a failure) stays linked
            // until the squad is home, so it isn't offered again while they walk back.
            if (e.opp.linkedSite is { Destroyed: true }) e.opp.linkedSite = null;
            // A temporary owner is removed with its site; a saved reference to it would not resolve on load.
            if (e.opp.faction is { temporary: true }) e.opp.faction = null;
        }

        e.report = report;
        int tiles = e.originMap != null ? Departure.Tiles(e.originMap, e.opp) : (int)Find.WorldGrid.ApproxDistanceInTiles(e.originTile, e.opp.tile);
        double back = TravelMath.BackDays(e.ride, e.tripFootDays, tiles, tuning.Travel);
        e.StartPhase(Phase.Returning, (int)(back * GenDate.TicksPerDay));
        if (!e.AwayPawns.Any(p => !p.Dead && p.RaceProps.Humanlike && !e.IsCaptive(p))) Arrival.Return(e);
        game.HolderChanged();
    }

    // Mods' effects on each pawn that came through the site: alive, humanlike, not captured.
    private static void PawnEffects(Expedition e, Outcome outcome, ExpeditionReport report)
    {
        ExpeditionSiteDef site = e.opp.site;
        if (site.pawnEffects.Count == 0) return;
        foreach (PawnResult r in report.pawns.ToList())
        {
            if (r.fate != PawnFate.Unharmed && r.fate != PawnFate.Injured) continue;
            Pawn p = r.pawn;
            if (p.Dead || !p.RaceProps.Humanlike) continue;
            foreach (PawnEffect pe in site.pawnEffects.Where(x => x.outcomes.Contains(outcome)))
                Effects.Safe(site, () => pe.Apply(p, e, outcome, report));
        }
    }

    // Alms given at the site: from everyone who carries, pack animals too, since their packs were counted at launch.
    private static void GiveSupplies(Expedition e, Outcome outcome, ExpeditionReport report)
    {
        if (e.opp.site.supplies is not { } s || !s.spentOn.Contains(outcome)) return;
        var carriers = e.AwayPawns.Where(p => !p.Dead && !p.IsPrisoner && !RideFinder.VehicleCompat.IsVehicle(p)).ToList();
        (float food, int meds) = SupplyPlanner.Spend(carriers, s.nutrition, s.medicine);
        if (food > 0f || meds > 0) report.effectLines.Add("Exp_SuppliesGivenLine".Translate(food.ToString("F1"), meds));
    }

    // The site's own outcome effects, then those a faction adds when it holds this site.
    public static IEnumerable<OutcomeEffect> OutcomeEffectsFor(Expedition e) =>
        e.opp.site.outcomeEffects.Concat(ExpeditionFactionDef.For(e.opp.enemy ?? e.opp.faction)?.EffectsFor(e.opp.site) ?? Enumerable.Empty<OutcomeEffect>());

    private static PawnResult ApplyFate(Expedition e, Pawn p, Outcome outcome, ExpeditionReport report)
    {
        ExpeditionSiteDef site = e.opp.site;
        PawnFate fate = OutcomeRoll.PickFate(site.risk.For(outcome), Rand.Value);
        // Pack animals are not taken; they come home hurt or not at all.
        if (!p.RaceProps.Humanlike && fate == PawnFate.Captured) fate = PawnFate.Killed;
        var result = new PawnResult { pawn = p, fate = fate };

        switch (fate)
        {
            case PawnFate.Unharmed:
                Wear(p, WearUnharmed);
                break;
            case PawnFate.Injured:
                result.injuries = InjuryMaker.Apply(p, Rand.Range(0.3f, 0.7f), outcome == Outcome.Failure);
                Wear(p, WearInjured);
                break;
            case PawnFate.Killed:
                Kill(e, p);
                return result;
            case PawnFate.Captured:
                if (!Capture(e, p)) goto case PawnFate.Injured;
                return result;
        }

        if (p.skills != null)
        {
            foreach (SiteChallenge sc in site.challenges)
            foreach (SkillDef skill in sc.challenge.skills)
            {
                if (p.skills.GetSkill(skill).TotallyDisabled) continue;
                float xp = ExpeditionsDefOf.Tuning.xpPerChallengeWeight * sc.weight * site.xpFactor;
                if (xp <= 0f) continue;
                p.skills.Learn(skill, xp, direct: true);
                result.xp[skill] = (result.xp.TryGetValue(skill, out float had) ? had : 0f) + xp;
            }
        }
        return result;
    }

    private static void Wear(Pawn p, float fraction)
    {
        var gear = new List<Thing>();
        if (p.apparel != null) gear.AddRange(p.apparel.WornApparel);
        if (p.equipment != null) gear.AddRange(p.equipment.AllEquipmentListForReading);
        foreach (Thing t in gear.Where(t => t.def.useHitPoints))
            t.HitPoints = Math.Max(1, t.HitPoints - (int)Math.Ceiling(t.MaxHitPoints * fraction));
    }

    internal static void Kill(Expedition e, Pawn p)
    {
        e.Forget(p);
        if (!Find.WorldPawns.Contains(p)) Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.KeepForever);
        p.Kill(null);
        if (p.RaceProps.Humanlike)
            Find.LetterStack.ReceiveLetter("Exp_DeathLabel".Translate(p.LabelShortCap),
                "Exp_DeathText".Translate(p.LabelShortCap, e.opp.Label), LetterDefOf.Death);
    }

    // Taken by the enemy the squad was sent against, else the site's faction if hostile, else any enemy faction.
    // Never a temporary faction (most work site owners): vanilla removes it once its site is gone, captives and all.
    // Vanilla's kidnapping then applies: they may be sold, recruited, or turn up in raids.
    private static bool Capture(Expedition e, Pawn p)
    {
        // Mechs, insects and the ancients (hidden factions) hold a quest site but take no prisoners.
        if (e.opp.linkedSite?.Faction is { Hidden: true }) return false;
        Faction? captor = e.opp.enemy is { defeated: false } en && en.HostileTo(Faction.OfPlayer) ? en
            : e.opp.faction is { defeated: false, temporary: false, Hidden: false } f && f.HostileTo(Faction.OfPlayer) ? f
            : Find.FactionManager.RandomEnemyFaction(false, false, true);
        if (captor?.kidnapped == null) return false;
        e.Forget(p);
        captor.kidnapped.Kidnap(p, null);
        ExpeditionsGame.Instance.capturedTick[p] = Find.TickManager.TicksGame;
        Find.LetterStack.ReceiveLetter("Exp_CapturedLabel".Translate(p.LabelShortCap),
            "Exp_CapturedText".Translate(p.LabelShortCap, captor.Name, e.opp.Label), LetterDefOf.NegativeEvent);
        return true;
    }

    private static void Rescue(Expedition e, Pawn captive, ExpeditionReport report)
    {
        if (CaptiveTracker.Free(captive)) e.contents.TryAdd(captive);
        report.pawns.Add(new PawnResult { pawn = captive, fate = PawnFate.Unharmed });
    }

    // Goodwill from the patron, then a little from every friendly faction. Halved on Partial, nothing on Failure.
    private static void Reward(Expedition e, Outcome outcome, ExpeditionReport report)
    {
        if (outcome == Outcome.Failure) return;
        ExpeditionSiteDef site = e.opp.site;
        int goodwill = outcome == Outcome.Partial ? HalfUp(site.goodwillReward) : site.goodwillReward;
        // Only a patron that still is one: a faction that turned hostile since the offer pays nothing.
        if (goodwill > 0 && e.opp.faction is { } patron && (site.needsFaction == FactionNeed.None || OfferGenerator.Wants(site.needsFaction, patron, site.factionDefs))
            && patron.TryAffectGoodwillWith(Faction.OfPlayer, goodwill, true, true, null, null))
        {
            report.goodwillFaction = patron;
            report.goodwill = goodwill;
        }

        int all = outcome == Outcome.Partial ? HalfUp(site.goodwillAllFriendly) : site.goodwillAllFriendly;
        if (all <= 0) return;
        foreach (Faction f in Find.FactionManager.AllFactionsListForReading.ToList())
        {
            if (!OfferGenerator.Wants(FactionNeed.Friendly, f)) continue;
            // The report carries this line; a message per faction would only be noise.
            if (f.TryAffectGoodwillWith(Faction.OfPlayer, all, false, true, null, null)) report.goodwillAll = all;
        }
    }

    private static int HalfUp(int n) => (n + 1) / 2;

    // Mechs woken on site: they answer to a mechanitor in the squad, and over bandwidth vanilla's rules apply.
    private static List<Pawn> Mechs(Expedition e, JoinerSpec j, ExpeditionReport report)
    {
        var made = new List<Pawn>();
        if (!ModsConfig.BiotechActive) return made;
        Pawn? mechanitor = e.AwayPawns.FirstOrDefault(p => !p.Dead && p.RaceProps.Humanlike && MechanitorUtility.IsMechanitor(p));
        List<PawnKindDef> kinds = j.LiveMechKinds();
        if (kinds.Count == 0)
        {
            report.effectLines.Add("Exp_MechsNoneAvailable".Translate());
            return made;
        }
        if (mechanitor == null)
        {
            report.effectLines.Add("Exp_MechsNoMechanitor".Translate());
            return made;
        }
        int n = j.count.RandomInRange;
        for (int i = 0; i < n; i++)
        {
            Pawn mech = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kinds.RandomElement(), Faction.OfPlayer,
                PawnGenerationContext.NonPlayer, tile: e.opp.tile, forceGenerateNewPawn: true));
            mechanitor.relations.AddDirectRelation(PawnRelationDefOf.Overseer, mech);
            made.Add(mech);
        }
        return made;
    }

    // People taken on site, made prisoners at once so they never fight for or with the squad. Only if someone of the
    // squad is left standing to bring them back. Whether they end as slaves is settled on arrival.
    private static void TakeCaptives(Expedition e, Outcome outcome, ExpeditionReport report)
    {
        if (e.opp.site.captives is not { } c || !Rand.Chance((float)HookMath.JoinerChance(c.chance, outcome, c.partialFactor))) return;
        if (!e.AwayPawns.Any(p => !p.Dead && p.RaceProps.Humanlike && !e.IsCaptive(p) && !RideFinder.VehicleCompat.IsVehicle(p))) return;
        Faction? faction = e.opp.faction is { defeated: false } f && !f.IsPlayer ? f : null;
        int n = c.count.RandomInRange;
        for (int i = 0; i < n; i++)
        {
            PawnKindDef kind = faction?.RandomPawnKind() ?? PawnKindDefOf.Villager;
            Pawn p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, faction, PawnGenerationContext.NonPlayer,
                tile: e.opp.tile, forceGenerateNewPawn: true));
            // Gear can't be dropped by a pawn that isn't on a map; nobody comes home armed, and it isn't loot.
            p.equipment?.DestroyAllEquipment();
            p.inventory?.DestroyAll();
            p.guest?.SetGuestStatus(Faction.OfPlayer, GuestStatus.Prisoner);
            if (!e.contents.TryAdd(p))
            {
                Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.Discard);
                continue;
            }
            e.captives.Add(p);
            report.pawns.Add(new PawnResult { pawn = p, fate = PawnFate.Taken });
        }
    }

    // People who join and animals tamed on site. They come home in the holder with the squad.
    private static void Recruits(Expedition e, Outcome outcome, ExpeditionReport report)
    {
        ExpeditionSiteDef site = e.opp.site;
        var made = new List<Pawn>();
        if (site.joiners is { } j && Rand.Chance((float)HookMath.JoinerChance(j.chance, outcome, j.partialFactor)))
        {
            if (j.mechKinds.Count > 0) made.AddRange(Mechs(e, j, report));
            else if (j.kind != null)
            {
                int n = j.count.RandomInRange;
                for (int i = 0; i < n; i++)
                    made.Add(PawnGenerator.GeneratePawn(new PawnGenerationRequest(j.kind, Faction.OfPlayer, PawnGenerationContext.NonPlayer,
                        tile: e.opp.tile, forceGenerateNewPawn: true)));
                // Joiners are made in the colony's faction, so they already hold its faith: a convert needs only the event.
                if (j.joinEvent != null)
                    foreach (Pawn joiner in made.Where(p => p.RaceProps.Humanlike))
                        Effects.Safe(site, () => Faith.Record(j.joinEvent, Faith.Doer(e)));
            }
        }
        if (outcome == Outcome.Success && site.tamedAnimals is { } a && Rand.Chance(a.chance))
        {
            BiomeDef biome = Find.WorldGrid[e.opp.tile].PrimaryBiome;
            List<PawnKindDef> kinds = a.Candidates(biome);
            int n = kinds.Count == 0 ? 0 : a.count.RandomInRange;
            for (int i = 0; i < n; i++)
            {
                // A listed herd isn't the biome's wildlife: its kinds may never live wild here, so pick evenly.
                PawnKindDef k = a.kinds.Count > 0 ? kinds.RandomElement() : kinds.RandomElementByWeight(biome.CommonalityOfAnimal);
                made.Add(PawnGenerator.GeneratePawn(new PawnGenerationRequest(k, Faction.OfPlayer, PawnGenerationContext.NonPlayer,
                    tile: e.opp.tile, forceGenerateNewPawn: true)));
            }
        }
        foreach (Pawn p in made)
        {
            if (!e.contents.TryAdd(p))
            {
                Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.Discard);
                continue;
            }
            report.pawns.Add(new PawnResult { pawn = p, fate = PawnFate.Joined });
        }
    }
}
