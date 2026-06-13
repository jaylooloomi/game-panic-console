using System.Collections.Generic;
using PanicConsole.Core;

namespace PanicConsole.Minigames
{
    /// <summary>空間規劃：方向鍵移動的貪食蛇。OnBlur 以 25% 速度沿當前方向直行。</summary>
    public class SnakeSim : MinigameBase
    {
        public int Width = 15;
        public int Height = 15;
        public float StepInterval = 0.15f;

        public struct Cell { public int X, Y; public Cell(int x, int y) { X = x; Y = y; } }

        private readonly List<Cell> _body = new List<Cell>();
        public IReadOnlyList<Cell> Body => _body;
        public int DirX { get; private set; }
        public int DirY { get; private set; }
        public Cell Food { get; private set; }

        private float _acc;
        private int _foodSeq;

        public SnakeSim() { GameId = "snake"; }

        public override void Reset()
        {
            _body.Clear();
            int cx = Width / 2, cy = Height / 2;
            _body.Add(new Cell(cx, cy));
            _body.Add(new Cell(cx - 1, cy));
            _body.Add(new Cell(cx - 2, cy));
            DirX = 1; DirY = 0;
            _acc = 0f; _foodSeq = 0;
            RespawnFood();
        }

        public void SetDirection(int dx, int dy)
        {
            if (dx == -DirX && dy == -DirY) return; // 不可反向
            if (dx != 0 && dy != 0) return;         // 只允許四方向
            DirX = dx; DirY = dy;
        }

        protected override void Simulate(float dt, bool isFocused)
        {
            float factor = isFocused ? 1f : 0.25f;
            _acc += dt * factor;
            while (_acc >= StepInterval)
            {
                _acc -= StepInterval;
                if (!Step(isFocused)) { _acc = 0f; break; }
            }
        }

        private bool Step(bool isFocused)
        {
            var head = _body[0];
            int nx = head.X + DirX, ny = head.Y + DirY;

            bool blocked = nx < 0 || nx >= Width || ny < 0 || ny >= Height;
            if (!blocked)
                for (int i = 0; i < _body.Count - 1; i++) // 撞自己（尾巴會讓位故排除）
                    if (_body[i].X == nx && _body[i].Y == ny) { blocked = true; break; }

            if (blocked)
            {
                if (isFocused) { TriggerFail(); return false; } // 前台撞牆/自身才失敗
                return true; // 背景：原地停住、不扣血（避免無法操控卻冤死）
            }

            _body.Insert(0, new Cell(nx, ny));
            if (nx == Food.X && ny == Food.Y) RespawnFood();
            else _body.RemoveAt(_body.Count - 1);
            return true;
        }

        private void RespawnFood()
        {
            // 決定性擺放（便於測試）；遊戲中視覺仍會四處出現
            _foodSeq++;
            int fx = (Food.X + 3 + _foodSeq) % Width;
            int fy = (Food.Y + 2 + _foodSeq) % Height;
            if (fx == 0 && fy == 0) fx = Width / 2;
            Food = new Cell(fx, fy);
        }
    }
}
