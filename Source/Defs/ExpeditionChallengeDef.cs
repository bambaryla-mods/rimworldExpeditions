using RimWorld;
using Verse;

namespace Expeditions;

// One stat (or skill level) a challenge reads, its weight, and the value a decent pawn has (scores 100).
// A skill is for what vanilla has no stat for, such as Artistic or Cooking.
public class ChallengeStat
{
    public StatDef? stat;
    public SkillDef? skill;
    public float weight = 1f;
    public float reference = 1f;
}

// A challenge is a weighted list of vanilla stats, combined across the squad by its rule.
public class ExpeditionChallengeDef : Def
{
    public List<ChallengeStat> stats = new();
    public CombineRule combine = CombineRule.Sum;
    public bool isStealth;
    public bool isHaul;
    public bool isSearch;
    public List<SkillDef> skills = new();

    public override IEnumerable<string> ConfigErrors()
    {
        foreach (string e in base.ConfigErrors()) yield return e;
        if (stats.NullOrEmpty()) yield return "has no stats";
        foreach (ChallengeStat s in stats)
        {
            if ((s.stat == null) == (s.skill == null)) yield return "has a stat entry that needs exactly one of stat or skill";
            if (s.reference <= 0f) yield return $"stat {s.stat?.defName ?? s.skill?.defName} has reference {s.reference}, it must be above 0";
        }
    }
}
