using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace Expeditions.Tests;

public class IdeoMathTests
{
    [Fact]
    public void A_match_opens_the_gate() => Assert.True(IdeoMath.GateOpen(true, false));

    [Fact]
    public void No_match_keeps_it_closed() => Assert.False(IdeoMath.GateOpen(false, false));

    [Fact]
    public void A_block_closes_it_even_with_a_match() => Assert.False(IdeoMath.GateOpen(true, true));

    // A gate whose every entry dropped out (its mods aren't loaded) has nothing to match: closed, not open to every faith.
    [Fact]
    public void Nothing_to_match_and_nothing_blocking_is_closed() => Assert.False(IdeoMath.GateOpen(false, false));

    // The report writes each fate as "Exp_Fate_" + its name; a fate with no key shows up as raw key text.
    [Fact]
    public void Every_fate_has_report_text()
    {
        XDocument keyed = XDocument.Load(Path.Combine(SourceDir(), "..", "..", "Languages", "English", "Keyed", "Expeditions.xml"));
        var keys = keyed.Root!.Elements().Select(e => e.Name.LocalName).ToHashSet();
        var missing = Enum.GetNames<PawnFate>().Where(f => !keys.Contains("Exp_Fate_" + f)).ToList();
        Assert.Empty(missing);
    }

    private static string SourceDir([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;
}
