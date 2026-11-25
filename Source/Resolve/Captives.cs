using RimWorld;
using Verse;

namespace Expeditions;

// What captives become when the squad gets home: slaves for a faith that keeps them and a free colonist to take them,
// prisoners otherwise. Settled on arrival, so a faith reformed while the squad was away gets its own answer.
public static class Captives
{
    public static void Settle(Expedition e, ExpeditionReport report)
    {
        var held = e.captives.Where(c => !c.Dead && e.contents.Contains(c) && c.IsPrisoner).ToList();
        if (held.Count == 0) return;
        bool slavery = ModsConfig.IdeologyActive && Faith.Primary?.PreceptsListForReading.Any(p => p.def.approvesOfSlavery) == true;
        Pawn? warden = e.AwayPawns.FirstOrDefault(p => Eligibility.IsOverlord(p) && !e.IsCaptive(p));
        if (slavery && warden != null)
        {
            int n = held.Count(c => GenGuest.TryEnslavePrisoner(warden, c));
            if (n > 0) report.effectLines.Add("Exp_CaptivesEnslavedLine".Translate(n));
            if (n < held.Count) report.effectLines.Add("Exp_CaptivesPrisonersLine".Translate(held.Count - n));
            return;
        }
        report.effectLines.Add((slavery ? "Exp_CaptivesNoWardenLine" : "Exp_CaptivesPrisonersLine").Translate(held.Count));
    }
}
