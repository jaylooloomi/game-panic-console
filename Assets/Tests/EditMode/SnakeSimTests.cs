using NUnit.Framework;
using PanicConsole.Core;
using PanicConsole.Minigames;

public class SnakeSimTests
{
    [Test] public void ResetCentersSnakeMovingRight()
    {
        var s = new SnakeSim(); s.Reset();
        Assert.AreEqual(3, s.Body.Count);
        Assert.AreEqual(1, s.DirX); Assert.AreEqual(0, s.DirY);
    }

    [Test] public void FocusedStepMovesHeadRight()
    {
        var s = new SnakeSim(); s.Reset(); s.OnFocus();
        var head = s.Body[0];
        s.Tick(s.StepInterval, true);
        Assert.AreEqual(head.X + 1, s.Body[0].X);
        Assert.AreEqual(head.Y, s.Body[0].Y);
    }

    [Test] public void BackgroundMovesAtQuarterSpeed()
    {
        var s = new SnakeSim(); s.Reset(); s.OnBlur();
        var head = s.Body[0];
        s.Tick(s.StepInterval, false);        // 0.25 step worth -> no move yet
        Assert.AreEqual(head.X, s.Body[0].X);
        s.Tick(s.StepInterval * 3f, false);   // total 1 step worth -> one move
        Assert.AreEqual(head.X + 1, s.Body[0].X);
    }

    [Test] public void HittingWallFails()
    {
        var s = new SnakeSim { Width = 10, Height = 10 }; s.Reset(); s.OnFocus();
        bool failed = false; s.OnFail += _ => failed = true;
        for (int i = 0; i < 20 && !failed; i++) s.Tick(s.StepInterval, true);
        Assert.IsTrue(failed);
    }

    [Test] public void CannotReverseDirection()
    {
        var s = new SnakeSim(); s.Reset(); s.OnFocus(); // moving right
        s.SetDirection(-1, 0); // try reverse -> ignored
        Assert.AreEqual(1, s.DirX);
    }
}
