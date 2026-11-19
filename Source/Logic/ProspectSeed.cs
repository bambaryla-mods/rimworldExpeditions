using System.Text;

// Logic: no Verse, RimWorld or UnityEngine here.
namespace Expeditions;

// Whether a lead exists is fixed by the world seed, the current window and the item, so asking again or reloading
// cannot change the answer. A new window opens every 2.5 days.
public static class ProspectSeed
{
    public const int WindowTicks = 150000;

    public static int WindowAt(long ticksAbs) => (int)(ticksAbs / WindowTicks);

    public static long TicksUntilNextWindow(long ticksAbs) => WindowTicks - ticksAbs % WindowTicks;

    // FNV-1a, not string.GetHashCode: that one is randomized per process, so it would change between sessions.
    public static double Roll01(int worldSeed, int window, string key)
    {
        uint hash = 2166136261;
        foreach (byte b in Encoding.UTF8.GetBytes($"{worldSeed}|{window}|{key}"))
        {
            hash ^= b;
            hash *= 16777619;
        }
        return hash / (double)uint.MaxValue;
    }

    public static bool HasLead(int worldSeed, int window, string key, double chance) => Roll01(worldSeed, window, key) < chance;
}
