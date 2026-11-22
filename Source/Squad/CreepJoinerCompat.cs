using HarmonyLib;
using RimWorld;
using Verse;

namespace Expeditions;

public enum CreepDue
{
    None,
    Leaves,
    Assault,
    Fleshbeasts,
    Sightstealers,
}

// Vanilla only checks a creepjoiner's downside while the pawn is on a map, so on an expedition it would wait and
// fire at home. The two that matter on the road (leaving and turning) are taken over here. The timer and the
// "already happened" flags are private, so they are read by reflection; if that breaks, creepjoiners are treated
// as ordinary colonists and vanilla fires their downside after they are home.
public static class CreepJoinerCompat
{
    private static bool resolved, broken;
    private static AccessTools.FieldRef<Pawn_CreepJoinerTracker, int>? triggersAt;
    private static AccessTools.FieldRef<Pawn_CreepJoinerTracker, bool>? triggeredDownside, triggeredAggressive, duplicated, hasLeft;

    public static CreepDue Due(Pawn p, int now)
    {
        if (!p.IsCreepJoiner || p.Dead || !Resolve()) return CreepDue.None;
        Pawn_CreepJoinerTracker t = p.creepjoiner;
        CreepJoinerDownsideDef? downside = t.downside;
        if (downside == null) return CreepDue.None;
        try
        {
            // Vanilla's own "disabled" cases: a duplicate, or a pawn whose ego was ever lost.
            if (triggeredDownside!(t) || duplicated!(t) || p.everLostEgo) return CreepDue.None;
            int at = triggersAt!(t);
            if (at == 0 || now < at) return CreepDue.None;
            if (downside.workerType == typeof(CreepJoinerWorker_DepartDownside)) return hasLeft!(t) ? CreepDue.None : CreepDue.Leaves;
            if (downside.workerType != typeof(CreepJoinerWorker_AggressiveDownside) || triggeredAggressive!(t)) return CreepDue.None;
            Type? kind = t.aggressive?.workerType;
            if (kind == typeof(CreepJoinerWorker_FleshbeastEmergence)) return CreepDue.Fleshbeasts;
            if (kind == typeof(CreepJoinerWorker_SightstealerAttack)) return CreepDue.Sightstealers;
            return CreepDue.Assault;
        }
        catch (Exception e)
        {
            SwitchOff(e.Message);
            return CreepDue.None;
        }
    }

    // So vanilla does not fire the same downside again once the pawn is back on a map.
    public static void MarkFired(Pawn p, CreepDue what)
    {
        if (!p.IsCreepJoiner || !Resolve()) return;
        Pawn_CreepJoinerTracker t = p.creepjoiner;
        try
        {
            triggeredDownside!(t) = true;
            if (what == CreepDue.Leaves) hasLeft!(t) = true;
            else triggeredAggressive!(t) = true;
        }
        catch (Exception e)
        {
            SwitchOff(e.Message);
        }
    }

    private static bool Resolve()
    {
        if (resolved) return !broken;
        resolved = true;
        try
        {
            triggersAt = AccessTools.FieldRefAccess<Pawn_CreepJoinerTracker, int>("downsideTriggersAt");
            triggeredDownside = AccessTools.FieldRefAccess<Pawn_CreepJoinerTracker, bool>("triggeredDownside");
            triggeredAggressive = AccessTools.FieldRefAccess<Pawn_CreepJoinerTracker, bool>("triggeredAggressive");
            duplicated = AccessTools.FieldRefAccess<Pawn_CreepJoinerTracker, bool>("duplicated");
            hasLeft = AccessTools.FieldRefAccess<Pawn_CreepJoinerTracker, bool>("hasLeft");
            return true;
        }
        catch (Exception e)
        {
            SwitchOff(e.Message);
            return false;
        }
    }

    private static void SwitchOff(string why)
    {
        if (broken && resolved) return;
        broken = true;
        Log.Warning($"[Expeditions] Creepjoiner support off: {why}. Their downsides fire after they are home.");
    }
}
