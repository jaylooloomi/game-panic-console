using System;
using System.Collections.Generic;
using NUnit.Framework;
using PanicConsole.Core;
using PanicConsole.Minigames;

public class OpponentAiTests
{
    static DuelEngine MakeEngine(Func<IMinigame> factory, int maxHp = 5, CardType[] pool = null)
    {
        var factories = new List<Func<IMinigame>> { factory };
        var e = new DuelEngine(factories, new SwitchTimer(), seed: 1, maxHp: maxHp, cardPool: pool)
        {
            OpeningInvincibility = 0f,
            PostFailInvincibility = 0f,
        };
        e.Start();
        return e;
    }

    // 兩側都掛 AI，跑固定時間（避免任一側「放生」即死，使對局提前結束）。
    static void RunBothAi(DuelEngine e, float difficulty, float seconds, float dt = 0.05f)
    {
        var a0 = new OpponentAi(e, 0, seed: 11, difficulty: difficulty);
        var a1 = new OpponentAi(e, 1, seed: 22, difficulty: difficulty);
        int steps = (int)(seconds / dt);
        for (int i = 0; i < steps && !e.IsOver; i++)
        {
            a0.Tick(dt); a1.Tick(dt);
            e.Tick(dt);
        }
    }

    [Test] public void DinoAiSurvivesAtMaxDifficulty()
    {
        var e = MakeEngine(() => new DinoSim());
        RunBothAi(e, 1f, 12f);
        Assert.AreEqual(5, e.Opponent.State.Hp, "滿級 AI 應跳過所有障礙、零失誤");
        Assert.Greater(e.Opponent.State.Score, 0, "應成功通過障礙得分");
    }

    [Test] public void ClickerAiHoldsPressureAtMaxDifficulty()
    {
        var e = MakeEngine(() => new ClickerSim());
        RunBothAi(e, 1f, 6f);
        Assert.AreEqual(5, e.Opponent.State.Hp, "滿級 AI 應狂點維持壓力、不歸零");
    }

    [Test] public void PianoAiScoresAtMaxDifficulty()
    {
        var e = MakeEngine(() => new PianoSim());
        RunBothAi(e, 1f, 6f);
        Assert.Greater(e.Opponent.State.Score, 0, "AI 應點掉落下的方塊得分");
        Assert.AreEqual(5, e.Opponent.State.Hp, "滿級 AI 不應讓方塊落地");
    }

    [Test] public void SnakeAiScoresAtMaxDifficulty()
    {
        var e = MakeEngine(() => new SnakeSim());
        RunBothAi(e, 1f, 8f);
        Assert.Greater(e.Opponent.State.Score, 0, "AI 應導向食物得分");
    }

    [Test] public void TetrisAiRunsAndSurvivesShortMatch()
    {
        var e = MakeEngine(() => new TetrisSim());
        Assert.DoesNotThrow(() => RunBothAi(e, 1f, 8f));
        Assert.Greater(e.Opponent.State.Hp, 0, "短時間內不應頂到頂端出局");
    }

    [Test] public void WeakAiDoesWorseThanStrongAi()
    {
        var strong = MakeEngine(() => new DinoSim(), maxHp: 99);
        RunBothAi(strong, 1f, 40f);

        var weak = MakeEngine(() => new DinoSim(), maxHp: 99);
        RunBothAi(weak, 0f, 40f);

        Assert.AreEqual(99, strong.Opponent.State.Hp, "滿級 AI 全程零失誤");
        Assert.Less(weak.Opponent.State.Hp, strong.Opponent.State.Hp, "弱 AI 應因失誤掉血、表現較差");
    }

    [Test] public void AiPlaysSabotageCardOnHuman()
    {
        var e = MakeEngine(() => new DinoSim(), maxHp: 30, pool: new[] { CardType.Ghost });
        var ai = new OpponentAi(e, 1, seed: 5, difficulty: 1f);
        e.Opponent.Cards.AddEnergy(100f); // 直接抽到一張女鬼
        Assert.AreEqual(1, e.Opponent.Cards.Hand.Count);

        bool humanGhosted = false;
        for (int i = 0; i < 200 && !e.IsOver; i++) // ~10s
        {
            ai.Tick(0.05f);
            e.Tick(0.05f);
            if (e.Player.IsGhosted) { humanGhosted = true; break; }
        }
        Assert.IsTrue(humanGhosted, "AI 應在冷卻後對玩家施放干擾卡");
        Assert.AreEqual(0, e.Opponent.Cards.Hand.Count, "卡應已出掉");
    }
}
