using NUnit.Framework;
using PanicConsole.Minigames;

public class BomberSimTests
{
    static BomberSim Make()
    {
        var b = new BomberSim();
        b.Reset();
        return b;
    }

    [Test] public void ResetPlacesTwoPlayersAtCornersAlive()
    {
        var b = Make();
        Assert.IsTrue(b.Players[0].Alive);
        Assert.IsTrue(b.Players[1].Alive);
        Assert.AreEqual(1, b.Players[0].X);
        Assert.AreEqual(1, b.Players[0].Y);
        Assert.IsFalse(b.IsOver);
    }

    [Test] public void BorderIsSolid()
    {
        var b = Make();
        Assert.AreEqual(BomberSim.Tile.Solid, b.Grid[0, 0]);
        Assert.AreEqual(BomberSim.Tile.Solid, b.Grid[b.Width - 1, b.Height - 1]);
    }

    [Test] public void MoveBlockedBySolidWall()
    {
        var b = Make();
        // P1 在 (1,1)，往左/下是邊界牆 → 不能動
        b.Move(0, -1, 0);
        b.Move(0, 0, -1);
        Assert.AreEqual(1, b.Players[0].X);
        Assert.AreEqual(1, b.Players[0].Y);
    }

    [Test] public void BombExplodesAfterFuseAndKillsPlayerInRange()
    {
        var b = Make();
        b.PlaceBomb(0);            // 炸彈放在 P1 腳下 (1,1)
        Assert.AreEqual(1, b.Bombs.Count);
        b.Tick(b.BombFuse + 0.01f); // 引爆
        Assert.AreEqual(0, b.Bombs.Count);
        Assert.IsFalse(b.Players[0].Alive); // 自己被炸死
        Assert.IsTrue(b.IsOver);
        Assert.AreEqual(2, b.Winner);       // P2 勝
    }

    [Test] public void ExplosionDestroysBrickButNotSolid()
    {
        var b = Make();
        // 在 (1,1) 右邊放一塊磚，炸彈炸掉它
        b.Grid[2, 1] = BomberSim.Tile.Brick;
        b.PlaceBomb(0);
        b.Tick(b.BombFuse + 0.01f);
        Assert.AreEqual(BomberSim.Tile.Empty, b.Grid[2, 1]); // 磚被炸掉
        Assert.AreEqual(BomberSim.Tile.Solid, b.Grid[0, 1]); // 牆還在
    }

    [Test] public void MoveBlockedByBomb()
    {
        var b = Make();
        b.PlaceBomb(0);        // 炸彈在 (1,1)
        b.Move(0, 1, 0);       // 移到 (2,1)
        b.Move(0, -1, 0);      // 想移回 (1,1) → 被炸彈擋住
        Assert.AreEqual(2, b.Players[0].X);
    }
}
