using NUnit.Framework;
using PanicConsole.Core;
using PanicConsole.Minigames;

public class DownstairsSimTests
{
    [Test] public void ResetPlacesPlayerAndPlatforms()
    {
        var d = new DownstairsSim(); d.Reset();
        Assert.AreEqual(d.Width / 2, d.PlayerCol);
        Assert.AreEqual(d.Height / 2, d.PlayerRow);
        Assert.Greater(d.Platforms.Count, 0);
        Assert.AreEqual(0, d.Steps);
    }

    [Test] public void BackgroundTickFreezes()
    {
        var d = new DownstairsSim(); d.Reset();
        int row = d.PlayerRow;
        d.Tick(2f, false);
        Assert.AreEqual(0, d.Steps);
        Assert.AreEqual(row, d.PlayerRow);
    }

    [Test] public void FocusedTickAdvancesSteps()
    {
        var d = new DownstairsSim(); d.Reset();
        d.Tick(d.StepInterval + 0.001f, true);
        Assert.GreaterOrEqual(d.Steps, 1);
    }

    [Test] public void MoveClampsWithinBounds()
    {
        var d = new DownstairsSim(); d.Reset();
        for (int i = 0; i < d.Width + 3; i++) d.MoveLeft();
        Assert.AreEqual(0, d.PlayerCol);
        for (int i = 0; i < d.Width + 3; i++) d.MoveRight();
        Assert.AreEqual(d.Width - 1, d.PlayerCol);
    }

    [Test] public void PlayerRowStaysInBoundsOverManySteps()
    {
        var d = new DownstairsSim(); d.Reset();
        bool failed = false; d.OnFail += _ => failed = true;
        for (int i = 0; i < 300 && !failed; i++)
        {
            d.Tick(d.StepInterval, true);
            Assert.GreaterOrEqual(d.PlayerRow, 0);
            Assert.Less(d.PlayerRow, d.Height);
        }
        // 不主動找缺口（不移動），最終應被頂到天花板而失敗
        Assert.IsTrue(failed, "長時間不操作應被平台頂到天花板而失敗");
    }
}
