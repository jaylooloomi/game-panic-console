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

        /// <summary>無敵剩餘秒數：開場熱身與失誤冷卻期間不扣血（避免冤死/連環扣）。</summary>
        public float InvincibleRemaining { get; private set; }
        public bool IsInvincible => InvincibleRemaining > 0f;

        public event Action OnGameOver;

        public MatchState(int maxHp = 5)
        {
            MaxHp = maxHp;
            Hp = maxHp;
        }

        public void LoseHp(int amount = 1)
        {
            if (IsGameOver) return;
            if (IsInvincible) return; // 無敵期間不扣血
            Hp = Math.Max(0, Hp - amount);
            if (IsGameOver) OnGameOver?.Invoke();
        }

        /// <summary>授予無敵（取較長者，不縮短現有）。</summary>
        public void GrantInvincibility(float seconds)
        {
            if (seconds > InvincibleRemaining) InvincibleRemaining = seconds;
        }

        public void TickInvincibility(float dt)
        {
            if (InvincibleRemaining > 0f) InvincibleRemaining = Math.Max(0f, InvincibleRemaining - dt);
        }

        public void AddScore(int points) { if (!IsGameOver) Score += points; }
        public void TickTime(float dt) { if (!IsGameOver) SurvivalTime += dt; }
    }
}
