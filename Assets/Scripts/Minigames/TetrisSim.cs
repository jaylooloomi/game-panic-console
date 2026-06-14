using System.Collections.Generic;
using PanicConsole.Core;

namespace PanicConsole.Minigames
{
    /// <summary>極限方塊（Tetris）：方塊下落、左右移動、旋轉、滿行消除。
    /// OnBlur 立即固定當前方塊（規格 §4.2）；OnFocus 後 0.5 秒解凍緩衝由基底處理。</summary>
    public class TetrisSim : MinigameBase
    {
        public struct Cell { public int X, Y; public Cell(int x, int y) { X = x; Y = y; } }
        static Cell C(int x, int y) => new Cell(x, y);

        public int Width = 8;
        public int Height = 16;
        public float DropInterval = 0.6f;

        // 7 種方塊（相對格，y 向上）。索引 1 為 O（不旋轉）。
        static readonly Cell[][] Shapes =
        {
            new[]{ C(-1,0), C(0,0), C(1,0), C(2,0) }, // I
            new[]{ C(0,0), C(1,0), C(0,1), C(1,1) },  // O
            new[]{ C(-1,0), C(0,0), C(1,0), C(0,1) }, // T
            new[]{ C(0,0), C(1,0), C(-1,1), C(0,1) }, // S
            new[]{ C(-1,1), C(0,1), C(0,0), C(1,0) }, // Z
            new[]{ C(-1,0), C(0,0), C(1,0), C(1,1) }, // L
            new[]{ C(-1,0), C(0,0), C(1,0), C(-1,1) },// J
        };

        bool[,] _grid;
        readonly List<Cell> _piece = new List<Cell>(4); // 當前方塊的相對格
        int _px, _py;
        bool _hasPiece;
        bool _isO;
        int _seq;
        float _acc;

        public TetrisSim() { GameId = "tetris"; }

        public bool[,] Grid => _grid;             // 已固定的格（渲染用）
        public IReadOnlyList<Cell> Piece => _piece; // 當前方塊相對格（世界座標 = +PieceX/PieceY）
        public int PieceX => _px;
        public int PieceY => _py;
        public bool HasPiece => _hasPiece;

        public override void Reset()
        {
            _grid = new bool[Width, Height];
            _piece.Clear();
            _hasPiece = false;
            _seq = 0;
            _acc = 0f;
        }

        public override void OnBlur()
        {
            base.OnBlur();
            if (_hasPiece) LockPiece(spawnNext: false); // 切走時立即固定，背景不再生成
        }

        public void MoveLeft() => TryMove(-1);
        public void MoveRight() => TryMove(1);

        void TryMove(int dx)
        {
            if (_hasPiece && Fits(_px + dx, _py, _piece)) _px += dx;
        }

        public void Rotate()
        {
            if (!_hasPiece || _isO) return;
            var rotated = new List<Cell>(4);
            foreach (var c in _piece) rotated.Add(new Cell(c.Y, -c.X)); // 順時針
            if (Fits(_px, _py, rotated)) { _piece.Clear(); _piece.AddRange(rotated); }
        }

        public void SoftDrop()
        {
            if (!_hasPiece) return;
            if (Fits(_px, _py - 1, _piece)) _py -= 1;
            else LockPiece(spawnNext: true);
        }

        protected override void Simulate(float dt, bool isFocused)
        {
            if (!isFocused) return; // 背景凍結
            if (!_hasPiece) { SpawnNext(); if (!_hasPiece) return; } // 生成失敗已 TriggerFail

            _acc += dt;
            while (_acc >= DropInterval)
            {
                _acc -= DropInterval;
                if (Fits(_px, _py - 1, _piece)) _py -= 1;
                else { LockPiece(spawnNext: true); break; }
            }
        }

        void SpawnNext()
        {
            int idx = _seq % Shapes.Length;
            _isO = idx == 1;
            _seq++;
            _piece.Clear();
            foreach (var c in Shapes[idx]) _piece.Add(c);
            _px = Width / 2;
            _py = Height - 2;
            _acc = 0f;
            if (!Fits(_px, _py, _piece)) { _hasPiece = false; TriggerFail(); return; } // 堆到頂 → 失敗
            _hasPiece = true;
        }

        void LockPiece(bool spawnNext)
        {
            foreach (var c in _piece)
            {
                int wx = _px + c.X, wy = _py + c.Y;
                if (wx >= 0 && wx < Width && wy >= 0 && wy < Height) _grid[wx, wy] = true;
            }
            _hasPiece = false;
            _piece.Clear();

            int cleared = ClearFullLines();
            if (cleared > 0) RaiseScore(cleared * 5);

            if (spawnNext) SpawnNext();
        }

        /// <summary>消除所有填滿的列、上方下移，回傳消除列數（公開以便測試）。</summary>
        public int ClearFullLines()
        {
            int cleared = 0;
            for (int y = 0; y < Height; y++)
            {
                bool full = true;
                for (int x = 0; x < Width; x++) if (!_grid[x, y]) { full = false; break; }
                if (!full) continue;

                cleared++;
                for (int yy = y; yy < Height - 1; yy++)
                    for (int x = 0; x < Width; x++) _grid[x, yy] = _grid[x, yy + 1];
                for (int x = 0; x < Width; x++) _grid[x, Height - 1] = false;
                y--; // 同列重新檢查（上方剛移下來）
            }
            return cleared;
        }

        bool Fits(int px, int py, List<Cell> cells)
        {
            foreach (var c in cells)
            {
                int wx = px + c.X, wy = py + c.Y;
                if (wx < 0 || wx >= Width || wy < 0) return false;
                if (wy < Height && _grid[wx, wy]) return false;
            }
            return true;
        }
    }
}
