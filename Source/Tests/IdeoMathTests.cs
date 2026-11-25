namespace Expeditions.Tests;

public class IdeoMathTests
{
    [Fact]
    public void A_listed_match_opens_the_gate() => Assert.True(IdeoMath.GateOpen(true, true, false));

    [Fact]
    public void Listed_but_unmatched_keeps_it_closed() => Assert.False(IdeoMath.GateOpen(true, false, false));

    [Fact]
    public void A_block_closes_it_even_with_a_match() => Assert.False(IdeoMath.GateOpen(true, true, true));

    [Fact]
    public void Nothing_listed_and_nothing_blocking_is_open() => Assert.True(IdeoMath.GateOpen(false, false, false));

    [Fact]
    public void Only_a_block_listed_and_hit_is_closed() => Assert.False(IdeoMath.GateOpen(false, false, true));

    [Fact]
    public void Taken_is_a_fate() => Assert.True(Enum.IsDefined(typeof(PawnFate), "Taken"));
}
