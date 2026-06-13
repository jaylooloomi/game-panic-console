using System;

namespace PanicConsole.Core
{
    /// <summary>20 秒強制切換的倒數計時器：管理輪數、警報與切換事件。</summary>
    public class SwitchTimer
    {
        public float WarningSeconds { get; }
        public int Round { get; private set; }
        public float Remaining { get; private set; }
        public bool WarningActive => Remaining <= WarningSeconds;

        public event Action OnWarning; // 進入倒數 3 秒時觸發一次
        public event Action OnSwitch;  // 倒數歸零時觸發

        /// <summary>切換間隔縮放（1 = 規格的 20/15/10；用於試玩調校節奏）。</summary>
        public float IntervalScale { get; }

        private bool _warningFired;

        public SwitchTimer(float warningSeconds = 3f, float intervalScale = 1f)
        {
            WarningSeconds = warningSeconds;
            IntervalScale = intervalScale <= 0f ? 1f : intervalScale;
            Round = 1;
            Remaining = IntervalForRound(Round) * IntervalScale;
        }

        public static float IntervalForRound(int round)
        {
            if (round <= 3) return 20f;
            if (round <= 6) return 15f;
            return 10f;
        }

        public void Tick(float dt)
        {
            if (dt < 0f) throw new ArgumentOutOfRangeException(nameof(dt));
            Remaining -= dt;

            if (!_warningFired && Remaining <= WarningSeconds)
            {
                _warningFired = true;
                OnWarning?.Invoke();
            }

            if (Remaining <= 0f)
            {
                Round++;
                Remaining = IntervalForRound(Round) * IntervalScale;
                _warningFired = false;
                OnSwitch?.Invoke();
            }
        }
    }
}
