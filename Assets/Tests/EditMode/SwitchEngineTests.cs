using System.Collections.Generic;
using NUnit.Framework;
using PanicConsole.Core;

public class SwitchEngineTests
{
    static SwitchEngine Make(out List<FakeMinigame> games, int hp = 5)
    {
        games = new List<FakeMinigame> { new FakeMinigame("a"), new FakeMinigame("b"), new FakeMinigame("c") };
        var list = new List<IMinigame>(games);
        return new SwitchEngine(list, new SwitchTimer(), new MatchState(hp));
    }

    [Test] public void StartFocusesFirstGameOnly()
    {
        var e = Make(out var games);
        e.Start();
        Assert.AreEqual(0, e.FocusIndex);
        Assert.IsTrue(games[0].IsFocused);
        Assert.IsFalse(games[1].IsFocused);
        Assert.IsFalse(games[2].IsFocused);
    }

    [Test] public void AllGamesTickEveryFrameRegardlessOfFocus()
    {
        var e = Make(out var games);
        e.Start();
        e.Tick(0.1f);
        Assert.AreEqual(1, games[0].SimTicks);
        Assert.AreEqual(1, games[1].SimTicks);
        Assert.AreEqual(1, games[2].SimTicks);
    }

    [Test] public void FocusAdvancesAfterInterval()
    {
        var e = Make(out var games);
        e.Start();
        e.Tick(20f); // cross the 20s interval
        Assert.AreEqual(1, e.FocusIndex);
        Assert.IsFalse(games[0].IsFocused);
        Assert.IsTrue(games[1].IsFocused);
    }

    [Test] public void FailDeductsHpAndResetsThatGame()
    {
        var e = Make(out var games);
        e.Start();
        int before = games[1].ResetCount; // Start() already called Reset once
        games[1].FailNow();
        Assert.AreEqual(4, e.State.Hp);
        Assert.AreEqual(before + 1, games[1].ResetCount);
    }

    [Test] public void TickIsNoopAfterGameOver()
    {
        var e = Make(out var games, hp: 1);
        e.Start();
        games[0].FailNow(); // hp 1 -> 0 -> game over
        int before = games[0].SimTicks;
        e.Tick(0.1f);
        Assert.AreEqual(before, games[0].SimTicks);
    }
}
