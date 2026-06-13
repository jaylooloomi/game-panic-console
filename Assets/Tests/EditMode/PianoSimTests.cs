using NUnit.Framework;
using PanicConsole.Core;
using PanicConsole.Minigames;

public class PianoSimTests
{
    [Test] public void ResetSpawnsOneTileAtTop()
    {
        var p = new PianoSim(); p.Reset();
        Assert.AreEqual(1, p.Tiles.Count);
        Assert.AreEqual(p.Height, p.Tiles[0].Y, 0.001f);
    }

    [Test] public void BackgroundTickFreezesTiles()
    {
        var p = new PianoSim(); p.Reset(); p.OnBlur();
        float y = p.Tiles[0].Y;
        p.Tick(1f, false);
        Assert.AreEqual(y, p.Tiles[0].Y, 0.001f);
        Assert.AreEqual(1, p.Tiles.Count); // 不生成
    }

    [Test] public void FocusedTickMovesTilesDown()
    {
        var p = new PianoSim(); p.Reset(); p.OnFocus();
        float y = p.Tiles[0].Y;
        p.Tick(0.05f, true);
        Assert.Less(p.Tiles[0].Y, y);
    }

    [Test] public void TileReachingBottomFails()
    {
        var p = new PianoSim(); p.Reset(); p.OnFocus();
        bool failed = false; p.OnFail += _ => failed = true;
        p.Tick(100f, true);
        Assert.IsTrue(failed);
    }

    [Test] public void HitRemovesTileInColumn()
    {
        var p = new PianoSim(); p.Reset(); p.OnFocus();
        int col = p.Tiles[0].Column;
        p.Hit(col);
        Assert.AreEqual(0, p.Tiles.Count);
    }

    [Test] public void HitEmptyColumnFails()
    {
        var p = new PianoSim(); p.Reset(); p.OnFocus();
        int occupied = p.Tiles[0].Column;
        int empty = (occupied + 1) % p.Columns;
        bool failed = false; p.OnFail += _ => failed = true;
        p.Hit(empty);
        Assert.IsTrue(failed);
    }
}
