using System;
using System.Collections.Generic;

namespace PanicConsole.Core
{
    /// <summary>核心循環編排：每幀推進計時器與所有小遊戲、切換焦點、處理失敗扣血。</summary>
    public class SwitchEngine
    {
        private readonly IReadOnlyList<IMinigame> _games;
        public int FocusIndex { get; private set; }
        public SwitchTimer Timer { get; }
        public MatchState State { get; }
        public IMinigame Focused => _games[FocusIndex];
        public IReadOnlyList<IMinigame> Games => _games;

        /// <summary>開場熱身無敵秒數（0 = 關閉，預設關閉以維持核心純粹；由 App 層按需開啟）。</summary>
        public float OpeningInvincibility = 0f;
        /// <summary>失誤後冷卻無敵秒數（0 = 關閉），避免同一遊戲瞬間連環扣血。</summary>
        public float PostFailInvincibility = 0f;
        /// <summary>開局的起始焦點索引（預設 0；App 可隨機化讓每個遊戲都有機會被先玩到）。</summary>
        public int StartFocusIndex = 0;

        public SwitchEngine(IReadOnlyList<IMinigame> games, SwitchTimer timer, MatchState state)
        {
            if (games == null || games.Count == 0)
                throw new ArgumentException("至少要有一個小遊戲", nameof(games));
            _games = games;
            Timer = timer ?? throw new ArgumentNullException(nameof(timer));
            State = state ?? throw new ArgumentNullException(nameof(state));

            Timer.OnSwitch += SwitchFocus;
            foreach (var g in _games)
            {
                g.OnFail += HandleFail;
                g.OnScore += HandleScore;
            }
            FocusIndex = 0;
        }

        public void Start()
        {
            foreach (var g in _games) { g.Init(); g.Reset(); }
            FocusIndex = ((StartFocusIndex % _games.Count) + _games.Count) % _games.Count;
            for (int i = 0; i < _games.Count; i++)
            {
                if (i == FocusIndex) _games[i].OnFocus();
                else _games[i].OnBlur();
            }
            if (OpeningInvincibility > 0f) State.GrantInvincibility(OpeningInvincibility);
        }

        public void Tick(float dt)
        {
            if (State.IsGameOver) return;
            State.TickTime(dt);
            State.TickInvincibility(dt);
            Timer.Tick(dt); // 可能透過事件呼叫 SwitchFocus
            for (int i = 0; i < _games.Count; i++)
                _games[i].Tick(dt, i == FocusIndex);
        }

        private void SwitchFocus()
        {
            _games[FocusIndex].OnBlur();
            FocusIndex = (FocusIndex + 1) % _games.Count;
            _games[FocusIndex].OnFocus();
        }

        private void HandleFail(IMinigame game)
        {
            State.LoseHp(1);
            if (PostFailInvincibility > 0f) State.GrantInvincibility(PostFailInvincibility);
            game.Reset();
        }

        private void HandleScore(int points) => State.AddScore(points);
    }
}
