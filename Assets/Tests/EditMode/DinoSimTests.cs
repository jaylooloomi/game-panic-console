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
        var d = new DinoSim(); d.Reset();
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
        var d = new DinoSim(); d.Reset();
        bool failed = false; d.OnFail += _ => failed = true;
        d.Tick(100f, true); // obstacle definitely reaches 0, not jumping
        Assert.IsTrue(failed);
    }

    [Test] public void JumpingClearsObstacleWithoutFail()
    {
        var d = new DinoSim { SpawnDistance = 6f, ScrollSpeed = 6f };
        d.Reset();
        bool failed = false; d.OnFail += _ => failed = true;
        d.Jump();               // airborne this frame
        d.Tick(1f, true);       // obstacle travels 6 units to 0 while airborne at frame start
        Assert.IsFalse(failed);
        Assert.AreEqual(d.SpawnDistance, d.ObstacleX, 0.001f); // respawned
    }

    // 規格 §7.1 / §6：剛取得焦點的 0.5 秒解凍緩衝期間，失敗相關模擬不得推進。
    [Test] public void OnFocusGraceSuppressesFailForHalfSecond()
    {
        var d = new DinoSim { SpawnDistance = 1f, ScrollSpeed = 100f };
        d.Reset();
        bool failed = false; d.OnFail += _ => failed = true;
        d.OnFocus();                  // 0.5 秒緩衝開始

        // 緩衝期內：即使 dt 大到足以把障礙撞上玩家，也以背景方式凍結，不得失敗。
        d.Tick(0.4f, true);
        Assert.IsFalse(failed, "緩衝期內不得推進失敗相關模擬");
        Assert.AreEqual(1f, d.ObstacleX, 0.0001f, "緩衝期內障礙應凍結，未推進");

        d.Tick(0.2f, true);           // 入口時緩衝仍 >0（0.1），仍視為背景
        Assert.IsFalse(failed);

        d.Tick(0.2f, true);           // 緩衝耗盡 → 正式前台，模擬恢復 → 障礙撞上失敗
        Assert.IsTrue(failed, "0.5 秒緩衝結束後，失敗相關模擬恢復");
    }
}
