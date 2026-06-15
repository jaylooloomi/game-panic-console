using System;
using System.Collections.Generic;
using NUnit.Framework;
using PanicConsole.Core;

public class DuelEngineTests
{
    // 用 FakeMinigame 當工廠，便於精確控制失敗/得分；id 依工廠索引固定。
    static List<Func<IMinigame>> Factories(int n = 3)
    {
        var list = new List<Func<IMinigame>>();
        for (int i = 0; i < n; i++)
        {
            int k = i;
            list.Add(() => new FakeMinigame("g" + k));
        }
        return list;
    }

    static DuelEngine Make(out FakeMinigame pCur, out FakeMinigame oCur,
        int maxHp = 5, float openingInv = 0f, CardType[] pool = null)
    {
        var e = new DuelEngine(Factories(), new SwitchTimer(), seed: 1, maxHp: maxHp, cardPool: pool)
        {
            OpeningInvincibility = openingInv,
            PostFailInvincibility = 0f,
        };
        e.Start();
        pCur = (FakeMinigame)e.Player.Current;
        oCur = (FakeMinigame)e.Opponent.Current;
        return e;
    }

    [Test] public void StartBuildsSameGameOnBothSides()
    {
        var e = Make(out var p, out var o);
        Assert.IsNotNull(p);
        Assert.IsNotNull(o);
        Assert.AreEqual(e.Player.Current.GameId, e.Opponent.Current.GameId, "兩邊應同步玩同一種遊戲");
        Assert.AreEqual(e.CurrentGameId, e.Player.Current.GameId);
    }

    [Test] public void BothSidesUseSeparateInstances()
    {
        var e = Make(out var p, out var o);
        Assert.AreNotSame(p, o, "兩邊各自獨立模擬，不能共用同一實例");
    }

    [Test] public void StartGrantsFullHpToBoth()
    {
        var e = Make(out _, out _, maxHp: 5);
        Assert.AreEqual(5, e.Player.State.Hp);
        Assert.AreEqual(5, e.Opponent.State.Hp);
        Assert.AreEqual(DuelResult.Ongoing, e.Result);
        Assert.IsFalse(e.IsOver);
    }

    [Test] public void SwitchAdvancesBothSidesSynchronously()
    {
        var e = Make(out var p0, out var o0);
        e.Tick(20f); // 跨過第一輪 20s
        Assert.AreNotSame(p0, e.Player.Current, "切換後玩家應換到新實例");
        Assert.AreNotSame(o0, e.Opponent.Current, "切換後對手應換到新實例");
        Assert.AreEqual(e.Player.Current.GameId, e.Opponent.Current.GameId, "切換後兩邊仍同步同一種遊戲");
    }

    [Test] public void FailDeductsHpOnThatSideOnly()
    {
        var e = Make(out var p, out var o);
        p.FailNow();
        Assert.AreEqual(4, e.Player.State.Hp);
        Assert.AreEqual(5, e.Opponent.State.Hp, "對手不應因玩家失誤而扣血");
    }

    [Test] public void ScoreIsRecordedPerSide()
    {
        var e = Make(out var p, out var o);
        p.ScoreNow(3);
        Assert.AreEqual(3, e.Player.State.Score);
        Assert.AreEqual(0, e.Opponent.State.Score);
    }

    [Test] public void PlayerWinsWhenOpponentHpReachesZero()
    {
        var e = Make(out var p, out var o, maxHp: 1);
        o.FailNow(); // 對手 1→0
        Assert.AreEqual(DuelResult.PlayerWins, e.Result);
        Assert.IsTrue(e.IsOver);
    }

    [Test] public void OpponentWinsWhenPlayerHpReachesZero()
    {
        var e = Make(out var p, out var o, maxHp: 1);
        p.FailNow();
        Assert.AreEqual(DuelResult.OpponentWins, e.Result);
    }

    [Test] public void OpeningInvincibilityProtectsBothSides()
    {
        var e = Make(out var p, out var o, maxHp: 5, openingInv: 2.5f);
        p.FailNow();
        o.FailNow();
        Assert.AreEqual(5, e.Player.State.Hp);
        Assert.AreEqual(5, e.Opponent.State.Hp);
        e.Tick(3f);   // 無敵到期
        ((FakeMinigame)e.Player.Current).FailNow();
        Assert.AreEqual(4, e.Player.State.Hp);
    }

    [Test] public void TickIsNoopAfterOver()
    {
        var e = Make(out var p, out var o, maxHp: 1);
        o.FailNow(); // game over
        int before = ((FakeMinigame)e.Opponent.Current).SimTicks;
        e.Tick(0.1f);
        Assert.AreEqual(before, ((FakeMinigame)e.Opponent.Current).SimTicks, "結束後不應再推進");
    }

    [Test] public void HealCardRestoresOwnHp()
    {
        var e = Make(out var p, out var o, maxHp: 5, pool: new[] { CardType.Heal });
        p.FailNow(); // 5→4
        e.Player.Cards.AddEnergy(100f); // 抽一張 Heal
        var played = e.PlayCard(0, 0);
        Assert.AreEqual(CardType.Heal, played);
        Assert.AreEqual(5, e.Player.State.Hp);
    }

    [Test] public void InvertCardAppliesToOpponentNotSelf()
    {
        var e = Make(out var p, out var o, pool: new[] { CardType.Invert });
        e.Player.Cards.AddEnergy(100f);
        var played = e.PlayCard(0, 0);
        Assert.AreEqual(CardType.Invert, played);
        Assert.IsTrue(e.Opponent.IsInverted, "干擾卡應作用在對手");
        Assert.IsFalse(e.Player.IsInverted, "干擾卡不應作用在自己");
    }

    [Test] public void InkCardAppliesToOpponentAndDecays()
    {
        var e = Make(out var p, out var o, pool: new[] { CardType.Ink });
        e.Player.Cards.AddEnergy(100f);
        e.PlayCard(0, 0);
        Assert.IsTrue(e.Opponent.IsInked);
        e.Tick(e.InkDuration + 0.1f);
        Assert.IsFalse(e.Opponent.IsInked, "效果應隨時間消退");
    }

    [Test] public void SlowMoCardSlowsOwnSimulation()
    {
        var e = Make(out var p, out var o, pool: new[] { CardType.SlowMo });
        e.Player.Cards.AddEnergy(100f);
        e.PlayCard(0, 0);
        Assert.IsTrue(e.Player.IsSlowed);
        Assert.IsFalse(e.Opponent.IsSlowed);
    }

    [Test] public void PlayCardReturnsNullWhenHandEmpty()
    {
        var e = Make(out _, out _);
        Assert.IsNull(e.PlayCard(0, 0));
    }
}
