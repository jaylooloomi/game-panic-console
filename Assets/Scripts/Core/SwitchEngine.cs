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

        public SwitchEngine(IReadOnlyList<IMinigame> games, SwitchTimer timer, MatchState state)
        {
            if (games == null || games.Count == 0)
                throw new ArgumentException("至少要有一個小遊戲", nameof(games));
            _games = games;
            Timer = timer ?? throw new ArgumentNullException(nameof(timer));
            State = state ?? throw new ArgumentNullException(nameof(state));

            Timer.OnSwitch += SwitchFocus;
            foreach (var g in _games) g.OnFail += HandleFail;
            FocusIndex = 0;
        }

        public void Start()
        {
            foreach (var g in _games) { g.Init(); g.Reset(); }
            for (int i = 0; i < _games.Count; i++)
            {
                if (i == FocusIndex) _games[i].OnFocus();
                else _games[i].OnBlur();
            }
        }

        public void Tick(float dt)
        {
            if (State.IsGameOver) return;
            State.TickTime(dt);
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
            game.Reset();
        }
    }
}
