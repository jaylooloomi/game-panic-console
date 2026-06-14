using NUnit.Framework;
using PanicConsole.Core;
using PanicConsole.Minigames;

public class ClickerSimTests
{
    [Test] public void ResetStartsWithSomePressure()
    {
        var c = new ClickerSim(); c.Reset();
        Assert.Greater(c.Pressure, 0f);
    }

    [Test] public void BackgroundTickDoesNotDrain()
    {
        var c = new ClickerSim(); c.Reset();
        float p = c.Pressure;
        c.Tick(5f, false);
        Assert.AreEqual(p, c.Pressure, 0.001f);
    }

    [Test] public void FocusedTickDrains()
    {
        var c = new ClickerSim(); c.Reset();
        float p = c.Pressure;
        c.Tick(1f, true);
        Assert.Less(c.Pressure, p);
    }

    [Test] public void ClickRaisesPressureCappedAtMax()
    {
        var c = new ClickerSim(); c.Reset();
        for (int i = 0; i < 100; i++) c.Click();
        Assert.AreEqual(c.Max, c.Pressure, 0.001f);
    }

    [Test] public void DrainingToZeroFails()
    {
        var c = new ClickerSim(); c.Reset();
        bool failed = false; c.OnFail += _ => failed = true;
        c.Tick(100f, true); // 長時間不點 → 壓力歸零
        Assert.IsTrue(failed);
    }
}
