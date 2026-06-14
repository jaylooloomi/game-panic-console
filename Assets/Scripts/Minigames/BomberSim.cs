using System.Collections.Generic;

namespace PanicConsole.Minigames
{
    /// <summary>爆爆王（炸彈超人）雙人對戰純邏輯：共用棋盤、放炸彈、十字爆風炸磚/炸死對手，最後存活者勝。
    /// 本身即雙人（非 MinigameBase），由 BomberGame(MonoBehaviour) 驅動；之後 Phase 3 可改由連線輸入驅動。</summary>
    public class BomberSim
    {
        public enum Tile { Empty, Solid, Brick }

        public int Width = 9;
        public int Height = 9;
        public float BombFuse = 2.0f;
        public int BombRange = 2;
        public float BlastDuration = 0.4f;

        public Tile[,] Grid { get; private set; }

        public struct Pawn { public int X, Y; public bool Alive; }
        public Pawn[] Players { get; private set; } = new Pawn[2];

        public class Bomb { public int X, Y, Owner, Range; public float Timer; }
        readonly List<Bomb> _bombs = new List<Bomb>();
        public IReadOnlyList<Bomb> Bombs => _bombs;

        // 目前爆風格（給渲染閃光用）
        readonly HashSet<int> _blast = new HashSet<int>();
        float _blastTimer;
        public bool IsBlast(int x, int y) => _blast.Contains(x * Height + y);

        public bool IsOver { get; private set; }
        public int Winner { get; private set; } // 0=未分, 1=P1, 2=P2, -1=平手

        int _brickSeq;

        public void Reset()
        {
            Grid = new Tile[Width, Height];
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                {
                    bool border = x == 0 || y == 0 || x == Width - 1 || y == Height - 1;
                    bool pillar = x % 2 == 0 && y % 2 == 0;
                    Grid[x, y] = (border || pillar) ? Tile.Solid : Tile.Empty;
                }
            // 散佈可破壞磚（決定性），但保留兩角落出生點淨空
            _brickSeq = 0;
            for (int x = 1; x < Width - 1; x++)
                for (int y = 1; y < Height - 1; y++)
                {
                    if (Grid[x, y] != Tile.Empty) continue;
                    if (IsSpawnSafe(x, y)) continue;
                    _brickSeq++;
                    if ((_brickSeq * 7 + 3) % 10 < 6) Grid[x, y] = Tile.Brick; // ~60% 機率
                }

            Players[0] = new Pawn { X = 1, Y = 1, Alive = true };
            Players[1] = new Pawn { X = Width - 2, Y = Height - 2, Alive = true };
            _bombs.Clear();
            _blast.Clear();
            _blastTimer = 0f;
            IsOver = false;
            Winner = 0;
        }

        bool IsSpawnSafe(int x, int y)
        {
            // 兩個出生點 (1,1) 與 (W-2,H-2) 及其相鄰格淨空，避免一出生就被磚困死
            return (x <= 2 && y <= 2) || (x >= Width - 3 && y >= Height - 3);
        }

        public void Move(int player, int dx, int dy)
        {
            if (IsOver || !Players[player].Alive) return;
            int nx = Players[player].X + dx, ny = Players[player].Y + dy;
            if (nx < 0 || nx >= Width || ny < 0 || ny >= Height) return;
            if (Grid[nx, ny] != Tile.Empty) return;
            if (BombAt(nx, ny) != null) return;
            var p = Players[player]; p.X = nx; p.Y = ny; Players[player] = p;
        }

        public void PlaceBomb(int player)
        {
            if (IsOver || !Players[player].Alive) return;
            int x = Players[player].X, y = Players[player].Y;
            if (BombAt(x, y) != null) return;
            _bombs.Add(new Bomb { X = x, Y = y, Owner = player, Range = BombRange, Timer = BombFuse });
        }

        Bomb BombAt(int x, int y)
        {
            foreach (var b in _bombs) if (b.X == x && b.Y == y) return b;
            return null;
        }

        public void Tick(float dt)
        {
            if (IsOver) return;

            if (_blastTimer > 0f)
            {
                _blastTimer -= dt;
                if (_blastTimer <= 0f) _blast.Clear();
            }

            var exploded = new List<Bomb>();
            foreach (var b in _bombs) { b.Timer -= dt; if (b.Timer <= 0f) exploded.Add(b); }
            foreach (var b in exploded) Explode(b);

            if (exploded.Count > 0) CheckWinner();
        }

        void Explode(Bomb b)
        {
            _bombs.Remove(b);
            _blast.Clear();
            _blastTimer = BlastDuration;

            AddBlast(b.X, b.Y);
            int[,] dirs = { { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 } };
            for (int d = 0; d < 4; d++)
            {
                for (int r = 1; r <= b.Range; r++)
                {
                    int x = b.X + dirs[d, 0] * r, y = b.Y + dirs[d, 1] * r;
                    if (x < 0 || x >= Width || y < 0 || y >= Height) break;
                    if (Grid[x, y] == Tile.Solid) break;            // 不可破壞牆擋住
                    AddBlast(x, y);
                    if (Grid[x, y] == Tile.Brick) { Grid[x, y] = Tile.Empty; break; } // 炸掉磚並停下
                }
            }

            // 炸到玩家 → 死亡
            for (int i = 0; i < 2; i++)
                if (Players[i].Alive && IsBlast(Players[i].X, Players[i].Y))
                {
                    var p = Players[i]; p.Alive = false; Players[i] = p;
                }
        }

        void AddBlast(int x, int y) => _blast.Add(x * Height + y);

        void CheckWinner()
        {
            bool a = Players[0].Alive, b = Players[1].Alive;
            if (a && b) return;
            IsOver = true;
            Winner = (!a && !b) ? -1 : (a ? 1 : 2);
        }
    }
}
