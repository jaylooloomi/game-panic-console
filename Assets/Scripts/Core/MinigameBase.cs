using System;

namespace PanicConsole.Core
{
    /// <summary>小遊戲抽象基底：管理 OnFail 管線與焦點狀態。具體模擬交給 Simulate。</summary>
    public abstract class MinigameBase : IMinigame
    {
        public const float FocusGraceSeconds = 0.5f;

        public string GameId { get; protected set; }
        public bool IsFocused { get; private set; }
        /// <summary>剛取得焦點後的解凍緩衝剩餘秒數（規格 §7：0.5 秒預警後恢復）。</summary>
        public float FocusGrace { get; private set; }
        public bool InFocusGrace => FocusGrace > 0f;

        public event Action<IMinigame> OnFail;
        public event Action<int> OnScore;

        public virtual void Init() { }
        public abstract void Reset();
        public virtual void OnFocus() { IsFocused = true; FocusGrace = FocusGraceSeconds; }
        public virtual void OnBlur() { IsFocused = false; FocusGrace = 0f; }

        public void Tick(float dt, bool isFocused)
        {
            // 解凍緩衝期間：雖視覺為前台，但以「背景」方式模擬，避免切回瞬間冤死。
            if (isFocused && FocusGrace > 0f)
            {
                FocusGrace -= dt;
                Simulate(dt, false);
                return;
            }
            Simulate(dt, isFocused);
        }

        protected abstract void Simulate(float dt, bool isFocused);
        protected void TriggerFail() => OnFail?.Invoke(this);
        protected void RaiseScore(int points) => OnScore?.Invoke(points);
    }
}
