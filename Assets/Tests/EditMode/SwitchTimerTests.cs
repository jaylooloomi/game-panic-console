using NUnit.Framework;
using PanicConsole.Core;

public class SwitchTimerTests
{
    [Test] public void StartsAtRoundOneWith20Seconds()
    {
        var t = new SwitchTimer();
        Assert.AreEqual(1, t.Round);
        Assert.AreEqual(20f, t.Remaining, 0.001f);
    }

    [Test] public void IntervalCurveByRound()
    {
        Assert.AreEqual(20f, SwitchTimer.IntervalForRound(1), 0.001f);
        Assert.AreEqual(20f, SwitchTimer.IntervalForRound(3), 0.001f);
        Assert.AreEqual(15f, SwitchTimer.IntervalForRound(4), 0.001f);
        Assert.AreEqual(15f, SwitchTimer.IntervalForRound(6), 0.001f);
        Assert.AreEqual(10f, SwitchTimer.IntervalForRound(7), 0.001f);
        Assert.AreEqual(10f, SwitchTimer.IntervalForRound(99), 0.001f);
    }

    [Test] public void TickReducesRemaining()
    {
        var t = new SwitchTimer();
        t.Tick(5f);
        Assert.AreEqual(15f, t.Remaining, 0.001f);
    }

    [Test] public void WarningFiresOnceWhenCrossingThreshold()
    {
        var t = new SwitchTimer();
        int warnings = 0; t.OnWarning += () => warnings++;
        t.Tick(16.5f); // remaining 3.5 -> no warn
        Assert.AreEqual(0, warnings);
        t.Tick(1f);    // remaining 2.5 -> warn
        Assert.AreEqual(1, warnings);
        t.Tick(0.5f);  // still in warning zone, no extra
        Assert.AreEqual(1, warnings);
    }

    [Test] public void SwitchFiresAtZeroAndAdvancesRound()
    {
        var t = new SwitchTimer();
        int switches = 0; t.OnSwitch += () => switches++;
        t.Tick(20f);
        Assert.AreEqual(1, switches);
        Assert.AreEqual(2, t.Round);
        Assert.AreEqual(20f, t.Remaining, 0.001f); // round 2 still 20
    }

    [Test] public void WarningReArmsAfterSwitch()
    {
        // 以小步前進（接近真實逐幀），避免單一大步同時跨越警報與切換
        var t = new SwitchTimer();
        int warnings = 0; t.OnWarning += () => warnings++;
        t.Tick(17f); // remaining 3 -> warn (round 1)
        Assert.AreEqual(1, warnings);
        t.Tick(3f);  // remaining 0 -> switch to round 2, re-arm
        t.Tick(18f); // remaining 2 -> warn again (round 2)
        Assert.AreEqual(2, warnings);
    }
}
