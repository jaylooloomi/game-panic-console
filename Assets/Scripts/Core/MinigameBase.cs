using System;

namespace PanicConsole.Core
{
    /// <summary>小遊戲抽象基底：管理 OnFail 管線與焦點狀態。具體模擬交給 Simulate。</summary>
    public abstract class MinigameBase : IMinigame
    {
        public string GameId { get; protected set; }
        public bool IsFocused { get; private set; }
        public event Action<IMinigame> OnFail;

        public virtual void Init() { }
        public abstract void Reset();
        public virtual void OnFocus() => IsFocused = true;
        public virtual void OnBlur() => IsFocused = false;

        public void Tick(float dt, bool isFocused) => Simulate(dt, isFocused);

        protected abstract void Simulate(float dt, bool isFocused);
        protected void TriggerFail() => OnFail?.Invoke(this);
    }
}
