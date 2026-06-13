using System;

namespace PanicConsole.Core
{
    /// <summary>所有小遊戲（含未來玩家自製）共同實作的合約。</summary>
    public interface IMinigame
    {
        string GameId { get; }
        bool IsFocused { get; }
        event Action<IMinigame> OnFail;

        void Init();
        void Reset();
        void OnFocus();
        void OnBlur();
        void Tick(float dt, bool isFocused); // 每幀呼叫，不論前後台
    }
}
