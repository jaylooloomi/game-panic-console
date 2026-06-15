using System;
using System.Collections.Generic;

namespace PanicConsole.Core
{
    /// <summary>1v1 對戰結果。</summary>
    public enum DuelResult { Ongoing, PlayerWins, OpponentWins, Draw }

    /// <summary>對戰中的一方：當前單一全螢幕小遊戲 + 自己的 HP/分數 + 卡牌 + 被施加的狀態效果。</summary>
    public class DuelSide
    {
        public string Name { get; }
        public MatchState State { get; }
        public CardEngine Cards { get; }

        /// <summary>當前正在玩的小遊戲（每次同步切換時換成同一種新遊戲的新實例）。</summary>
        public IMinigame Current { get; internal set; }

        // 狀態效果剩餘秒數（由對手的干擾卡施加；慢動作為自助）。
        public float GhostRemaining { get; internal set; }   // 女鬼閃現：畫面被遮蔽嚇人
        public float InvertRemaining { get; internal set; }  // 方向反轉：操作上下左右顛倒
        public float InkRemaining { get; internal set; }     // 噴墨：畫面被墨汁遮蔽
        public float SlowRemaining { get; internal set; }    // 慢動作（自助）：自己這側模擬變慢，較好反應

        public bool IsGhosted => GhostRemaining > 0f;
        public bool IsInverted => InvertRemaining > 0f;
        public bool IsInked => InkRemaining > 0f;
        public bool IsSlowed => SlowRemaining > 0f;
        /// <summary>被干擾（反轉或噴墨）—— 供 AI 判斷自己是否該「表現變差」。</summary>
        public bool IsDisrupted => IsInverted || IsInked;

        internal DuelSide(string name, int maxHp, CardType[] cardPool)
        {
            Name = name;
            State = new MatchState(maxHp);
            Cards = new CardEngine(cardPool);
        }

        internal void TickStatus(float dt)
        {
            if (GhostRemaining > 0f) GhostRemaining = Math.Max(0f, GhostRemaining - dt);
            if (InvertRemaining > 0f) InvertRemaining = Math.Max(0f, InvertRemaining - dt);
            if (InkRemaining > 0f) InkRemaining = Math.Max(0f, InkRemaining - dt);
            if (SlowRemaining > 0f) SlowRemaining = Math.Max(0f, SlowRemaining - dt);
        }

        internal void ApplyGhost(float s) { if (s > GhostRemaining) GhostRemaining = s; }
        internal void ApplyInvert(float s) { if (s > InvertRemaining) InvertRemaining = s; }
        internal void ApplyInk(float s) { if (s > InkRemaining) InkRemaining = s; }
        internal void ApplySlow(float s) { if (s > SlowRemaining) SlowRemaining = s; }
    }

    /// <summary>
    /// 1v1 對戰核心（純邏輯）：左=玩家、右=AI，各自全螢幕玩「同一種」小遊戲；
    /// 共用一個 SwitchTimer 固定秒數同步切換，每輪兩邊一起換到同一種新遊戲；
    /// 任一邊失誤扣血，先掉到 0 HP 的一方輸、對手贏。干擾卡施加到對手側。
    /// </summary>
    public class DuelEngine
    {
        public static readonly CardType[] DuelCardPool =
        {
            CardType.Shield, CardType.Heal, CardType.SlowMo, // 自助
            CardType.Ghost, CardType.Invert, CardType.Ink,   // 干擾對手
        };

        readonly IReadOnlyList<Func<IMinigame>> _factories;
        readonly int[] _order;       // 洗牌後的遊戲出場順序（循環使用）
        int _orderIndex;

        readonly DuelSide[] _sides;
        public DuelSide Player => _sides[0];
        public DuelSide Opponent => _sides[1];
        public IReadOnlyList<DuelSide> Sides => _sides;

        public SwitchTimer Timer { get; }
        public DuelResult Result { get; private set; } = DuelResult.Ongoing;
        public bool IsOver => Result != DuelResult.Ongoing;

        /// <summary>兩邊當前正在玩的遊戲 Id（兩邊同步、必定相同）。</summary>
        public string CurrentGameId => Player.Current?.GameId;

        // 可調參數（App 層按需設定）。
        public float OpeningInvincibility = 2.5f;
        public float PostFailInvincibility = 1.2f;
        public float GhostDuration = 1.2f;
        public float InvertDuration = 4f;
        public float InkDuration = 4f;
        public float SlowMoDuration = 3f;
        public float SlowMoFactor = 0.4f;
        public float ShieldDuration = 3f;
        /// <summary>每次成功得分換算成卡牌能量（與單人版一致）。</summary>
        public float EnergyPerScore = 20f;

        readonly int[] _lastScore = new int[2];

        public DuelEngine(IReadOnlyList<Func<IMinigame>> factories, SwitchTimer timer,
            int seed = 1, int maxHp = 5, CardType[] cardPool = null)
        {
            if (factories == null || factories.Count == 0)
                throw new ArgumentException("至少要有一個小遊戲工廠", nameof(factories));
            _factories = factories;
            Timer = timer ?? throw new ArgumentNullException(nameof(timer));

            _order = BuildOrder(factories.Count, seed);

            var pool = cardPool ?? DuelCardPool;
            _sides = new[]
            {
                new DuelSide("你", maxHp, pool),
                new DuelSide("對手", maxHp, pool),
            };

            Timer.OnSwitch += Advance;
        }

        static int[] BuildOrder(int n, int seed)
        {
            var order = new int[n];
            for (int i = 0; i < n; i++) order[i] = i;
            var rng = new Random(seed);
            for (int i = n - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            return order;
        }

        public void Start()
        {
            _orderIndex = 0;
            Result = DuelResult.Ongoing;
            for (int s = 0; s < 2; s++)
            {
                _sides[s].Cards.Reset();
                _lastScore[s] = 0;
                SetGame(s, _order[_orderIndex]);
                if (OpeningInvincibility > 0f) _sides[s].State.GrantInvincibility(OpeningInvincibility);
            }
        }

        public void Tick(float dt)
        {
            if (IsOver) return;

            Timer.Tick(dt); // 可能觸發 Advance（同步切換兩邊）

            for (int s = 0; s < 2; s++)
            {
                var side = _sides[s];
                if (side.State.IsGameOver) continue;

                side.TickStatus(dt);
                side.State.TickTime(dt);
                side.State.TickInvincibility(dt);

                float simDt = side.IsSlowed ? dt * SlowMoFactor : dt;
                side.Current.Tick(simDt, true); // 全螢幕單一遊戲：永遠是前台

                // 得分→蓄力卡牌能量
                int score = side.State.Score;
                if (score > _lastScore[s])
                {
                    side.Cards.AddEnergy((score - _lastScore[s]) * EnergyPerScore);
                    _lastScore[s] = score;
                }
            }

            EvaluateResult();
        }

        void EvaluateResult()
        {
            bool p = Player.State.IsGameOver;
            bool o = Opponent.State.IsGameOver;
            if (p && o) Result = DuelResult.Draw;
            else if (o) Result = DuelResult.PlayerWins;
            else if (p) Result = DuelResult.OpponentWins;
        }

        void Advance()
        {
            _orderIndex++;
            int fi = _order[_orderIndex % _order.Length];
            for (int s = 0; s < 2; s++)
                if (!_sides[s].State.IsGameOver)
                    SetGame(s, fi);
        }

        void SetGame(int sideIndex, int factoryIndex)
        {
            var side = _sides[sideIndex];
            var old = side.Current;
            if (old != null)
            {
                old.OnFail -= side_OnFailHandlers[sideIndex];
                old.OnScore -= side_OnScoreHandlers[sideIndex];
            }

            var game = _factories[factoryIndex]();
            side.Current = game;

            // 為每一側建立穩定的 handler 參考，方便切換時解除訂閱。
            EnsureHandlers(sideIndex);
            game.OnFail += side_OnFailHandlers[sideIndex];
            game.OnScore += side_OnScoreHandlers[sideIndex];

            game.Init();
            game.Reset();
            game.OnFocus(); // 啟動 0.5s 解凍緩衝，避免切換瞬間冤死
        }

        // 每側固定的事件處理委派（便於 -=）。
        readonly Action<IMinigame>[] side_OnFailHandlers = new Action<IMinigame>[2];
        readonly Action<int>[] side_OnScoreHandlers = new Action<int>[2];

        void EnsureHandlers(int sideIndex)
        {
            if (side_OnFailHandlers[sideIndex] == null)
                side_OnFailHandlers[sideIndex] = _ => HandleFail(sideIndex);
            if (side_OnScoreHandlers[sideIndex] == null)
                side_OnScoreHandlers[sideIndex] = pts => HandleScore(sideIndex, pts);
        }

        void HandleFail(int sideIndex)
        {
            var side = _sides[sideIndex];
            side.State.LoseHp(1);
            if (side.State.IsGameOver) { EvaluateResult(); return; } // HP 歸零即定勝負，不再重置
            if (PostFailInvincibility > 0f) side.State.GrantInvincibility(PostFailInvincibility);
            side.Current.Reset(); // 失誤後同一遊戲重來，撐到下次切換
        }

        void HandleScore(int sideIndex, int points) => _sides[sideIndex].State.AddScore(points);

        /// <summary>出 sideIndex 這方手牌第 slot 張；自助卡作用於自己、干擾卡作用於對手。回傳出掉的卡（無則 null）。</summary>
        public CardType? PlayCard(int sideIndex, int slot)
        {
            if (IsOver) return null;
            var side = _sides[sideIndex];
            var played = side.Cards.Play(slot);
            if (!played.HasValue) return null;
            ApplyCard(sideIndex, played.Value);
            return played;
        }

        void ApplyCard(int sideIndex, CardType card)
        {
            var self = _sides[sideIndex];
            var foe = _sides[1 - sideIndex];
            switch (card)
            {
                case CardType.Shield: self.State.GrantInvincibility(ShieldDuration); break;
                case CardType.Heal: self.State.Heal(1); break;
                case CardType.SlowMo: self.ApplySlow(SlowMoDuration); break;
                case CardType.Ghost: foe.ApplyGhost(GhostDuration); break;
                case CardType.Invert: foe.ApplyInvert(InvertDuration); break;
                case CardType.Ink: foe.ApplyInk(InkDuration); break;
                case CardType.Freeze: Timer.Freeze(3f); break; // 對戰版一般不入池；保留相容
            }
        }
    }
}
