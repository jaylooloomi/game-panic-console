using PanicConsole.Core;

namespace PanicConsole.Minigames
{
    /// <summary>反射神經：地面捲動、按跳躲避障礙。OnBlur 完全凍結。</summary>
    public class DinoSim : MinigameBase
    {
        public float SpawnDistance = 20f;
        public float ScrollSpeed = 6f;   // 放寬：障礙慢一點，新玩家較易反應
        public float JumpDuration = 0.6f; // 放寬：跳躍滯空久一點，判定更寬鬆

        public float ObstacleX { get; private set; }
        public bool IsAirborne => _airborne > 0f;
        private float _airborne;

        public DinoSim() { GameId = "dino"; }

        public override void Reset()
        {
            ObstacleX = SpawnDistance;
            _airborne = 0f;
        }

        public void Jump()
        {
            if (_airborne <= 0f) _airborne = JumpDuration;
        }

        protected override void Simulate(float dt, bool isFocused)
        {
            if (!isFocused) return; // 背景凍結

            bool airborneThisFrame = _airborne > 0f;
            ObstacleX -= ScrollSpeed * dt;
            if (ObstacleX <= 0f)
            {
                if (airborneThisFrame) ObstacleX = SpawnDistance; // 安全通過，重生
                else TriggerFail();
            }
            if (_airborne > 0f) _airborne -= dt;
        }
    }
}
