using NUnit.Framework;
using PanicConsole.Core;
using PanicConsole.Minigames;

public class TetrisSimTests
{
    static int Filled(TetrisSim t)
    {
        int n = 0;
        for (int x = 0; x < t.Width; x++)
            for (int y = 0; y < t.Height; y++)
                if (t.Grid[x, y]) n++;
        return n;
    }

    [Test] public void FocusedTickSpawnsPiece()
    {
        var t = new TetrisSim(); t.Reset();
        t.Tick(0.01f, true);
        Assert.IsTrue(t.HasPiece);
        Assert.AreEqual(4, t.Piece.Count);
    }

    [Test] public void BackgroundTickDoesNotSpawnOrMove()
    {
        var t = new TetrisSim(); t.Reset();
        t.Tick(1f, false);
        Assert.IsFalse(t.HasPiece);
        Assert.AreEqual(0, Filled(t));
    }

    [Test] public void GravityDropsPieceOverTime()
    {
        var t = new TetrisSim(); t.Reset();
        t.Tick(0.01f, true);
        int y0 = t.PieceY;
        t.Tick(t.DropInterval + 0.01f, true);
        Assert.Less(t.PieceY, y0);
    }

    [Test] public void MoveLeftShiftsPiece()
    {
        var t = new TetrisSim(); t.Reset();
        t.Tick(0.01f, true);
        int x0 = t.PieceX;
        t.MoveLeft();
        Assert.AreEqual(x0 - 1, t.PieceX);
    }

    [Test] public void RotateChangesOrientation()
    {
        var t = new TetrisSim(); t.Reset();
        t.Tick(0.01f, true); // 序號 0 = I（水平，所有相對 Y 為 0）
        foreach (var c in t.Piece) Assert.AreEqual(0, c.Y);
        t.Rotate();
        bool anyVertical = false;
        foreach (var c in t.Piece) if (c.Y != 0) anyVertical = true;
        Assert.IsTrue(anyVertical);
    }

    [Test] public void OnBlurLocksCurrentPiece()
    {
        var t = new TetrisSim(); t.Reset();
        t.Tick(0.01f, true);
        int before = Filled(t);
        t.OnBlur();
        Assert.IsFalse(t.HasPiece);
        Assert.AreEqual(before + 4, Filled(t));
    }

    [Test] public void SpawnBlockedTriggersFail()
    {
        var t = new TetrisSim(); t.Reset();
        for (int x = 0; x < t.Width; x++)
        {
            t.Grid[x, t.Height - 1] = true;
            t.Grid[x, t.Height - 2] = true;
        }
        bool failed = false; t.OnFail += _ => failed = true;
        t.Tick(0.01f, true);
        Assert.IsTrue(failed);
    }

    [Test] public void ClearFullLinesRemovesRow()
    {
        var t = new TetrisSim(); t.Reset();
        for (int x = 0; x < t.Width; x++) t.Grid[x, 0] = true;
        int cleared = t.ClearFullLines();
        Assert.AreEqual(1, cleared);
        for (int x = 0; x < t.Width; x++) Assert.IsFalse(t.Grid[x, 0]);
    }
}
