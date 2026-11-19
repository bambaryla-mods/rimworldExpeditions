namespace Expeditions.Tests;

public class OutcomeRollTests
{
    [Fact]
    public void Roll_bands()
    {
        Assert.Equal(Outcome.Success, OutcomeRoll.Roll(0.6, 0.5));
        Assert.Equal(Outcome.Partial, OutcomeRoll.Roll(0.6, 0.55));
        Assert.Equal(Outcome.Failure, OutcomeRoll.Roll(0.6, 0.7));
    }

    [Fact]
    public void PickFate_cumulative()
    {
        var w = new FateWeights(1, 1, 1, 1);
        Assert.Equal(PawnFate.Unharmed, OutcomeRoll.PickFate(w, 0.1));
        Assert.Equal(PawnFate.Injured, OutcomeRoll.PickFate(w, 0.3));
        Assert.Equal(PawnFate.Killed, OutcomeRoll.PickFate(w, 0.6));
        Assert.Equal(PawnFate.Captured, OutcomeRoll.PickFate(w, 0.9));
        Assert.Equal(PawnFate.Unharmed, OutcomeRoll.PickFate(new(0, 0, 0, 0), 0.9));
    }
}
