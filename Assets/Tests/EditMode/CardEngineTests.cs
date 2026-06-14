using NUnit.Framework;
using PanicConsole.Core;

public class CardEngineTests
{
    [Test] public void StartsEmpty()
    {
        var e = new CardEngine();
        Assert.AreEqual(0f, e.Energy, 0.001f);
        Assert.AreEqual(0, e.Hand.Count);
    }

    [Test] public void FullEnergyDrawsOneCardAndResetsEnergy()
    {
        var e = new CardEngine();
        e.AddEnergy(100f);
        Assert.AreEqual(1, e.Hand.Count);
        Assert.AreEqual(0f, e.Energy, 0.001f);
    }

    [Test] public void OverflowCarriesIntoNextCharge()
    {
        var e = new CardEngine();
        e.AddEnergy(150f);
        Assert.AreEqual(1, e.Hand.Count);
        Assert.AreEqual(50f, e.Energy, 0.001f); // 餘額保留
    }

    [Test] public void HandCapsAtMaxSlots()
    {
        var e = new CardEngine { MaxSlots = 3 };
        e.AddEnergy(1000f);            // 想抽很多
        Assert.AreEqual(3, e.Hand.Count); // 上限 3
    }

    [Test] public void PlayRemovesCardAndReturnsIt()
    {
        var e = new CardEngine();
        e.AddEnergy(100f);
        var played = e.Play(0);
        Assert.IsTrue(played.HasValue);
        Assert.AreEqual(0, e.Hand.Count);
    }

    [Test] public void PlayInvalidIndexReturnsNull()
    {
        var e = new CardEngine();
        Assert.IsNull(e.Play(0));
        Assert.IsNull(e.Play(-1));
    }

    [Test] public void DrawEventFires()
    {
        var e = new CardEngine();
        int draws = 0; e.OnDraw += _ => draws++;
        e.AddEnergy(100f);
        Assert.AreEqual(1, draws);
    }
}
