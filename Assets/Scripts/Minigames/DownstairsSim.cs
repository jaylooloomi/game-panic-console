using System.Collections.Generic;
using PanicConsole.Core;

namespace PanicConsole.Minigames
{
    /// <summary>小朋友下樓梯（單井，純邏輯）：平台持續上升，玩家要靠左右移動穿過缺口往下逃；
    /// 被平台頂到天花板尖刺即失敗。對戰版＝並排兩個此 sim，先被頂到頂者輸。
    /// 也可作為單人輪替的小遊戲（規格 §4.4）。</summary>
    public class DownstairsSim : MinigameBase
    {
        public struct Platform
        {
            public int Row; public int GapCol;
            public Platform(int row, int gapCol) { Row = row; GapCol = gapCol; }
        }

        public int Width = 7;
        public int Height = 12;     // 第 Height-1 列＝天花板尖刺（致命）
        public int GapWidth = 2;    // 每片平台的缺口寬
        public int Spacing = 3;     // 平台垂直間距
        public float StepInterval = 0.32f;

        readonly List<Platform> _plats = new List<Platform>();
        public IReadOnlyList<Platform> Platforms => _plats;
        public int PlayerCol { get; private set; }
        public int PlayerRow { get; private set; }
        public int Steps { get; private set; } // 已上升步數（測試/除錯用）

        float _acc;
        int _gapSeq;

        public DownstairsSim() { GameId = "downstairs"; }

        public override void Reset()
        {
            _plats.Clear();
            _gapSeq = 0; Steps = 0; _acc = 0f;
            for (int r = 1; r < Height - 1; r += Spacing) _plats.Add(new Platform(r, NextGap()));
            PlayerCol = Width / 2;
            PlayerRow = Height / 2;
        }

        int NextGap()
        {
            int range = Width - GapWidth + 1;       // 缺口起點可放的範圍
            int g = (_gapSeq * 3 + 1) % range;
            _gapSeq++;
            return g;
        }

        bool SolidAt(int col, int row)
        {
            foreach (var p in _plats)
                if (p.Row == row)
                {
                    bool inGap = col >= p.GapCol && col < p.GapCol + GapWidth;
                    if (!inGap) return true;
                }
            return false;
        }

        public void MoveLeft() { if (PlayerCol > 0) PlayerCol--; }
        public void MoveRight() { if (PlayerCol < Width - 1) PlayerCol++; }

        protected override void Simulate(float dt, bool isFocused)
        {
            if (!isFocused) return; // 背景凍結（切走時角色凍結）
            _acc += dt;
            while (_acc >= StepInterval)
            {
                _acc -= StepInterval;
                if (!Step()) { _acc = 0f; break; }
            }
        }

        bool Step()
        {
            // 1. 平台上升一列
            for (int i = 0; i < _plats.Count; i++)
                _plats[i] = new Platform(_plats[i].Row + 1, _plats[i].GapCol);
            Steps++;

            // 被升上來的平台頂到 → 玩家被推上一列
            if (SolidAt(PlayerCol, PlayerRow)) PlayerRow++;

            // 移除升出頂端者；底部依間距補新平台
            _plats.RemoveAll(p => p.Row >= Height);
            if (Steps % Spacing == 0) _plats.Add(new Platform(0, NextGap()));

            // 2. 重力：正下方非實心 → 往下掉一列（穿過缺口）
            if (PlayerRow > 0 && !SolidAt(PlayerCol, PlayerRow - 1)) PlayerRow--;

            // 3. 判敗：被頂到天花板尖刺
            if (PlayerRow >= Height - 1) { TriggerFail(); return false; }
            return true;
        }
    }
}
