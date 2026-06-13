using System;

namespace PanicConsole.Core
{
    /// <summary>對局共享狀態：HP、生存時間、分數、勝負。</summary>
    public class MatchState
    {
        public int MaxHp { get; }
        public int Hp { get; private set; }
        public float SurvivalTime { get; private set; }
        public int Score { get; private set; }
        public bool IsGameOver => Hp <= 0;

        public event Action OnGameOver;

        public MatchState(int maxHp = 5)
        {
            MaxHp = maxHp;
            Hp = maxHp;
        }

        public void LoseHp(int amount = 1)
        {
            if (IsGameOver) return;
            Hp = Math.Max(0, Hp - amount);
            if (IsGameOver) OnGameOver?.Invoke();
        }

        public void AddScore(int points) { if (!IsGameOver) Score += points; }
        public void TickTime(float dt) { if (!IsGameOver) SurvivalTime += dt; }
    }
}
