using System.Reflection;
using HarmonyLib;
using Verse;

namespace Expeditions;

// Comm Recruiter hirees, read by reflection: neither mod depends on the other. If Comm Recruiter changes and the
// members below are gone, this switches itself off with one warning, and hirees are then refused like any other
// quest guest.
public static class CommRecruiterCompat
{
    private const string ComponentType = "CommRecruiter.GameComponent_CommRecruiter";

    private static bool resolved, broken;
    private static Type? componentType;
    private static MethodInfo? recordFor;
    private static PropertyInfo? ticksLeft;
    private static FieldInfo? retained;

    // True when the pawn is on a Comm Recruiter contract.
    public static bool TryGetHiree(Pawn p, out int ticksLeftOut, out bool retainedOut)
    {
        ticksLeftOut = 0;
        retainedOut = false;
        if (!Resolve() || Current.Game == null) return false;
        try
        {
            GameComponent? component = Current.Game.components.FirstOrDefault(c => c.GetType() == componentType);
            if (component == null) return false;
            object? record = recordFor!.Invoke(component, new object[] { p });
            if (record == null) return false;
            ticksLeftOut = (int)ticksLeft!.GetValue(record)!;
            retainedOut = (bool)retained!.GetValue(record)!;
            return true;
        }
        catch (Exception e)
        {
            SwitchOff(e.Message);
            return false;
        }
    }

    private static bool Resolve()
    {
        if (resolved) return !broken;
        resolved = true;
        componentType = GenTypes.GetTypeInAnyAssembly(ComponentType);
        if (componentType == null)
        {
            broken = true; // not installed: nothing to say
            return false;
        }
        recordFor = AccessTools.Method(componentType, "RecordFor", new[] { typeof(Pawn) });
        Type? recordType = recordFor?.ReturnType;
        ticksLeft = recordType == null ? null : AccessTools.Property(recordType, "TicksLeft");
        retained = recordType == null ? null : AccessTools.Field(recordType, "retained");
        if (recordFor == null || ticksLeft == null || retained == null)
        {
            SwitchOff("RecordFor, TicksLeft or retained not found");
            return false;
        }
        return true;
    }

    private static void SwitchOff(string why)
    {
        broken = true;
        Log.Warning($"[Expeditions] CommRecruiter support off: {why}. Hirees are refused on expeditions.");
    }
}
