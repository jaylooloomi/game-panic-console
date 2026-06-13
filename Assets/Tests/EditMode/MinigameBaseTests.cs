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
}
