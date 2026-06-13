using NUnit.Framework;
using PanicConsole.Core;

public class MatchStateTests
{
    [Test] public void StartsWithFullHp()
    {
        var s = new MatchState();
        Assert.AreEqual(5, s.MaxHp);
        Assert.AreEqual(5, s.Hp);
        Assert.IsFalse(s.IsGameOver);
    }

    [Test] public void LoseHpDecrements()
    {
        var s = new MatchState();
        s.LoseHp();
        Assert.AreEqual(4, s.Hp);
    }

    [Test] public void GameOverWhenHpReachesZero()
    {
        var s = new MatchState(2);
        int over = 0; s.OnGameOver += () => over++;
        s.LoseHp(); s.LoseHp();
        Assert.IsTrue(s.IsGameOver);
        Assert.AreEqual(1, over);
    }

    [Test] public void HpNeverNegativeAndLoseAfterOverIsNoop()
    {
        var s = new MatchState(1);
        s.LoseHp(); s.LoseHp();
        Assert.AreEqual(0, s.Hp);
    }

    [Test] public void ScoreAndTimeAccumulateUntilGameOver()
    {
        var s = new MatchState(1);
        s.AddScore(10); s.TickTime(2f);
        Assert.AreEqual(10, s.Score);
        Assert.AreEqual(2f, s.SurvivalTime, 0.001f);
        s.LoseHp(); // game over
        s.AddScore(10); s.TickTime(2f);
        Assert.AreEqual(10, s.Score);
        Assert.AreEqual(2f, s.SurvivalTime, 0.001f);
    }
}
