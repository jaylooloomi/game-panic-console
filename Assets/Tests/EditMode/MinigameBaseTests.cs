using NUnit.Framework;
using PanicConsole.Core;

public class MinigameBaseTests
{
    [Test] public void TickInvokesSimulateWithFocusFlag()
    {
        var g = new FakeMinigame();
        g.Tick(0.1f, true);
        Assert.AreEqual(1, g.SimTicks);
        Assert.IsTrue(g.LastTickFocused);
        g.Tick(0.1f, false);
        Assert.AreEqual(2, g.SimTicks);
        Assert.IsFalse(g.LastTickFocused);
    }

    [Test] public void FocusBlurTogglesIsFocused()
    {
        var g = new FakeMinigame();
        g.OnFocus(); Assert.IsTrue(g.IsFocused);
        g.OnBlur();  Assert.IsFalse(g.IsFocused);
    }

    [Test] public void TriggerFailRaisesOnFailWithSelf()
    {
        var g = new FakeMinigame();
        IMinigame failed = null;
        g.OnFail += m => failed = m;
        g.FailNow();
        Assert.AreSame(g, failed);
    }

    [Test] public void FocusGraceSimulatesAsBackgroundThenLive()
    {
        var g = new FakeMinigame();
        g.OnFocus();
        Assert.IsTrue(g.InFocusGrace);
        g.Tick(0.1f, true);            // 緩衝期：以背景方式模擬
        Assert.IsFalse(g.LastTickFocused);
        g.Tick(0.5f, true);            // 緩衝在此 tick 入口仍 >0，仍背景
        Assert.IsFalse(g.LastTickFocused);
        g.Tick(0.1f, true);            // 緩衝已耗盡 → 正式前台
        Assert.IsTrue(g.LastTickFocused);
    }

    [Test] public void ScoreEventRaised()
    {
        var g = new FakeMinigame();
        int total = 0;
        g.OnScore += p => total += p;
        g.ScoreNow(3);
        Assert.AreEqual(3, total);
    }
}
