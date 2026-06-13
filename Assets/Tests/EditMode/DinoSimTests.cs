using NUnit.Framework;
using PanicConsole.Core;
using PanicConsole.Minigames;

public class DinoSimTests
{
    [Test] public void ResetPlacesObstacleAtSpawn()
    {
        var d = new DinoSim();
        d.Reset();
        Assert.AreEqual(d.SpawnDistance, d.ObstacleX, 0.001f);
    }

    [Test] public void FocusedTickMovesObstacleLeft()
    {
        var d = new DinoSim(); d.Reset(); d.OnFocus();
        d.Tick(1f, true);
        Assert.Less(d.ObstacleX, d.SpawnDistance);
    }

    [Test] public void BackgroundTickFreezesObstacle()
    {
        var d = new DinoSim(); d.Reset(); d.OnBlur();
        d.Tick(1f, false);
        Assert.AreEqual(d.SpawnDistance, d.ObstacleX, 0.001f);
    }

    [Test] public void ObstacleReachingPlayerWhileGroundedFails()
    {
        var d = new DinoSim(); d.Reset(); d.OnFocus();
        bool failed = false; d.OnFail += _ => failed = true;
        d.Tick(100f, true); // obstacle definitely reaches 0, not jumping
        Assert.IsTrue(failed);
    }

    [Test] public void JumpingClearsObstacleWithoutFail()
    {
        var d = new DinoSim { SpawnDistance = 6f, ScrollSpeed = 6f };
        d.Reset(); d.OnFocus();
        bool failed = false; d.OnFail += _ => failed = true;
        d.Jump();               // airborne this frame
        d.Tick(1f, true);       // obstacle travels 6 units to 0 while airborne at frame start
        Assert.IsFalse(failed);
        Assert.AreEqual(d.SpawnDistance, d.ObstacleX, 0.001f); // respawned
    }
}
