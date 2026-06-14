using System;
using System.Collections.Generic;

namespace PanicConsole.Core
{
    /// <summary>卡牌類型。單人版先用自助型（Shield/Heal/Freeze/SlowMo）；
    /// 干擾對手型（Ghost/Invert/Ink）共用同一引擎，於對戰/連線模式套用到對手。</summary>
    public enum CardType { Shield, Heal, Freeze, SlowMo, Ghost, Invert, Ink }

    /// <summary>能量與卡牌引擎（純邏輯）：得分累積能量，集滿抽一張卡到手牌（上限），可出牌。</summary>
    public class CardEngine
    {
        public float EnergyMax = 100f;
        public int MaxSlots = 3;

        public float Energy { get; private set; }
        readonly List<CardType> _hand = new List<CardType>();
        public IReadOnlyList<CardType> Hand => _hand;

        public event Action<CardType> OnDraw;

        readonly CardType[] _pool;
        int _drawSeq;

        public CardEngine(CardType[] pool = null)
        {
            _pool = pool ?? new[] { CardType.Shield, CardType.Heal, CardType.Freeze, CardType.SlowMo };
        }

        public void Reset()
        {
            Energy = 0f;
            _hand.Clear();
            _drawSeq = 0;
        }

        public void AddEnergy(float amount)
        {
            if (amount <= 0f) return;
            if (_hand.Count >= MaxSlots) return; // 手牌滿就不再蓄力
            Energy += amount;
            while (Energy >= EnergyMax && _hand.Count < MaxSlots)
            {
                Energy -= EnergyMax;
                Draw();
            }
            if (_hand.Count >= MaxSlots) Energy = 0f;
        }

        void Draw()
        {
            var c = _pool[_drawSeq % _pool.Length];
            _drawSeq++;
            _hand.Add(c);
            OnDraw?.Invoke(c);
        }

        /// <summary>出第 index 張手牌；成功回傳卡片、失敗回傳 null。</summary>
        public CardType? Play(int index)
        {
            if (index < 0 || index >= _hand.Count) return null;
            var c = _hand[index];
            _hand.RemoveAt(index);
            return c;
        }
    }
}
