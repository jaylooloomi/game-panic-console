using System;
using PanicConsole.Core;

namespace PanicConsole.Minigames
{
    /// <summary>
    /// 模擬對手：自動操作自己這一側當前的小遊戲（左=玩家、右=AI 即由本類別驅動）。
    /// 各遊戲有專屬啟發式；難度（0~1）調整反應速度與失誤率；被反轉/噴墨時表現變差；偶爾出干擾卡。
    /// 純邏輯、可由 dotnet 測試。使用方式：每幀在 engine.Tick 之前呼叫 Tick(dt)。
    /// </summary>
    public class OpponentAi
    {
        readonly DuelEngine _engine;
        readonly int _sideIndex;
        readonly Random _rng;

        /// <summary>0 = 很弱（慢、常失誤）；1 = 完美（快、不失誤）。</summary>
        public float Difficulty;

        DuelSide Side => _engine.Sides[_sideIndex];

        // 內部狀態（換遊戲時重置）。
        IMinigame _lastGame;
        float _think;       // 規劃型遊戲（蛇/方塊）的思考節拍
        float _clickAcc;    // 連點節拍
        bool _dinoArmed;    // 恐龍：本輪障礙是否尚未處理
        float _cardCd;      // 出牌冷卻

        public OpponentAi(DuelEngine engine, int sideIndex, int seed = 20260614, float difficulty = 0.7f)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _sideIndex = sideIndex;
            _rng = new Random(seed);
            Difficulty = Math.Max(0f, Math.Min(1f, difficulty));
            _cardCd = 2.5f;
        }

        float Lerp(float a, float b, float t) => a + (b - a) * t;
        bool Roll(float p) => _rng.NextDouble() < p;

        /// <summary>基礎失誤率（難度越高越低；被反轉/噴墨時額外提高）。</summary>
        float MistakeChance()
        {
            float m = 0.35f * (1f - Difficulty);
            if (Side.IsDisrupted) m += 0.4f;
            return Math.Min(0.9f, m);
        }

        public void Tick(float dt)
        {
            if (_engine.IsOver) return;
            var side = Side;
            if (side.State.IsGameOver) return;

            var game = side.Current;
            if (!ReferenceEquals(game, _lastGame))
            {
                _lastGame = game;
                _think = 0f; _clickAcc = 0f; _dinoArmed = true;
            }

            switch (game)
            {
                case DinoSim dino: PlayDino(dino); break;
                case ClickerSim clicker: PlayClicker(clicker, dt); break;
                case PianoSim piano: PlayPiano(piano); break;
                case SnakeSim snake: TickThink(dt, () => PlaySnake(snake)); break;
                case TetrisSim tetris: TickThink(dt, () => PlayTetris(tetris)); break;
            }

            MaybePlayCard(dt);
        }

        void TickThink(float dt, Action act)
        {
            _think += dt;
            float interval = Lerp(0.28f, 0.06f, Difficulty);
            while (_think >= interval) { _think -= interval; act(); }
        }

        // ---------- 反射類（逐幀） ----------
        void PlayDino(DinoSim dino)
        {
            float lead = dino.ScrollSpeed * Lerp(0.6f, 0.4f, Difficulty); // 觸發距離
            if (dino.ObstacleX > lead * 1.6f) _dinoArmed = true;          // 新障礙進場、重新待命
            if (_dinoArmed && !dino.IsAirborne && dino.ObstacleX <= lead && dino.ObstacleX > 0f)
            {
                _dinoArmed = false;
                if (!Roll(MistakeChance())) dino.Jump();                 // 失誤＝這次不跳
            }
        }

        void PlayClicker(ClickerSim clicker, float dt)
        {
            _clickAcc += dt;
            float interval = Lerp(0.42f, 0.12f, Difficulty);
            while (_clickAcc >= interval)
            {
                _clickAcc -= interval;
                if (!Roll(MistakeChance())) clicker.Click();             // 失誤＝漏一下
            }
        }

        void PlayPiano(PianoSim piano)
        {
            var tiles = piano.Tiles;
            if (tiles.Count == 0) return;
            int idx = -1; float min = float.MaxValue;
            for (int i = 0; i < tiles.Count; i++)
                if (tiles[i].Y < min) { min = tiles[i].Y; idx = i; }

            float threshold = Lerp(1.0f, 3.0f, Difficulty); // 難度高→提早點，較安全
            if (min <= threshold)
            {
                if (Roll(MistakeChance())) return;          // 失誤＝猶豫（可能讓方塊落地）
                piano.Hit(tiles[idx].Column);
            }
        }

        // ---------- 規劃類（依思考節拍） ----------
        void PlaySnake(SnakeSim snake)
        {
            var body = snake.Body;
            if (body.Count == 0) return;
            var head = body[0];
            int gx = snake.Food.X - head.X;
            int gy = snake.Food.Y - head.Y;

            // 候選方向：先較大落差的軸，再另一軸，最後沿用現方向。
            var cands = new System.Collections.Generic.List<(int dx, int dy)>(4);
            void Add(int dx, int dy) { if (dx != 0 || dy != 0) cands.Add((dx, dy)); }
            if (Math.Abs(gx) >= Math.Abs(gy)) { Add(Math.Sign(gx), 0); Add(0, Math.Sign(gy)); }
            else { Add(0, Math.Sign(gy)); Add(Math.Sign(gx), 0); }
            Add(snake.DirX, snake.DirY);
            Add(1, 0); Add(-1, 0); Add(0, 1); Add(0, -1); // 保底

            (int dx, int dy) chosen = (snake.DirX, snake.DirY);
            bool mistake = Roll(MistakeChance());
            foreach (var c in cands)
            {
                int dx = c.dx, dy = c.dy;
                if (mistake) continue; // 失誤＝不主動轉向，沿用現方向
                if (dx == -snake.DirX && dy == -snake.DirY) continue; // 不可反向
                if (!SafeNext(snake, dx, dy)) continue;
                chosen = (dx, dy);
                break;
            }

            int fx = chosen.dx, fy = chosen.dy;
            if (Side.IsInverted) { fx = -fx; fy = -fy; } // 被反轉：意圖方向顛倒（自亂陣腳）
            snake.SetDirection(fx, fy);
        }

        static bool SafeNext(SnakeSim snake, int dx, int dy)
        {
            var head = snake.Body[0];
            int nx = head.X + dx, ny = head.Y + dy;
            if (nx < 0 || nx >= snake.Width || ny < 0 || ny >= snake.Height) return false;
            var body = snake.Body;
            for (int i = 0; i < body.Count - 1; i++)
                if (body[i].X == nx && body[i].Y == ny) return false;
            return true;
        }

        void PlayTetris(TetrisSim t)
        {
            if (!t.HasPiece) return;
            // 找最矮的欄當落點目標。
            int target = LowestColumn(t);
            if (Side.IsInverted && Roll(0.7f)) target = t.Width - 1 - target; // 被反轉：偏向錯誤欄

            if (Roll(MistakeChance() * 0.5f)) { t.Rotate(); return; }

            if (t.PieceX < target) t.MoveRight();
            else if (t.PieceX > target) t.MoveLeft();
            else if (!Roll(MistakeChance())) t.SoftDrop(); // 對準了就往下壓
        }

        static int LowestColumn(TetrisSim t)
        {
            var grid = t.Grid;
            int best = 0, bestH = int.MaxValue;
            for (int x = 0; x < t.Width; x++)
            {
                int h = 0;
                for (int y = t.Height - 1; y >= 0; y--) if (grid[x, y]) { h = y + 1; break; }
                if (h < bestH) { bestH = h; best = x; }
            }
            return best;
        }

        // ---------- 卡牌 ----------
        void MaybePlayCard(float dt)
        {
            _cardCd -= dt;
            if (_cardCd > 0f) return;
            var hand = Side.Cards.Hand;
            if (hand.Count == 0) return;

            int slot = ChooseCardSlot();
            if (slot < 0) return;
            _engine.PlayCard(_sideIndex, slot);
            _cardCd = Lerp(6f, 2.5f, Difficulty);
        }

        int ChooseCardSlot()
        {
            var hand = Side.Cards.Hand;
            // 受傷且有補血 → 先補；否則優先干擾對手；再不然出第一張。
            bool hurt = Side.State.Hp < Side.State.MaxHp;
            int sabotage = -1, heal = -1;
            for (int i = 0; i < hand.Count; i++)
            {
                var c = hand[i];
                if (c == CardType.Ghost || c == CardType.Invert || c == CardType.Ink) { if (sabotage < 0) sabotage = i; }
                else if (c == CardType.Heal) { if (heal < 0) heal = i; }
            }
            if (hurt && heal >= 0) return heal;
            if (sabotage >= 0) return sabotage;
            return 0;
        }
    }
}
