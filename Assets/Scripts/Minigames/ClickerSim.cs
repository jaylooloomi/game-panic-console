using PanicConsole.Core;

namespace PanicConsole.Minigames
{
    /// <summary>連點挑戰（規格 §4.12）：壓力值持續下降，必須狂點維持；歸零即失敗。OnBlur 凍結。</summary>
    public class ClickerSim : MinigameBase
    {
        public float Max = 100f;
        public float DrainPerSec = 16f;
        public float ClickGain = 7f;

        public float Pressure { get; private set; }
        public float Ratio => Max > 0f ? Pressure / Max : 0f;

        public ClickerSim() { GameId = "clicker"; }

        public override void Reset() { Pressure = Max * 0.6f; }

        public void Click()
        {
            Pressure += ClickGain;
            if (Pressure > Max) Pressure = Max;
        }

        protected override void Simulate(float dt, bool isFocused)
        {
            if (!isFocused) return; // 背景凍結
            Pressure -= DrainPerSec * dt;
            if (Pressure <= 0f) { Pressure = 0f; TriggerFail(); }
        }
    }
}
