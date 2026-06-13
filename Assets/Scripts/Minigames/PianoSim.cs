using System.Collections.Generic;
using PanicConsole.Core;

namespace PanicConsole.Minigames
{
    /// <summary>節奏精準：黑塊由上落下，需在落地前點掉正確直行。OnBlur 凍結。</summary>
    public class PianoSim : MinigameBase
    {
        public int Columns = 4;
        public float Height = 10f;
        public float FallSpeed = 4f;
        public float SpawnInterval = 0.8f;

        public class Tile { public int Column; public float Y; }

        private readonly List<Tile> _tiles = new List<Tile>();
        public IReadOnlyList<Tile> Tiles => _tiles;

        private float _spawnAcc;
        private int _spawnSeq;

        public PianoSim() { GameId = "piano"; }

        public override void Reset()
        {
            _tiles.Clear();
            _spawnAcc = 0f; _spawnSeq = 0;
            Spawn(); // 起始給一塊在頂端
        }

        public void Hit(int column)
        {
            // 找該行最接近底部（Y 最小）的方塊
            int idx = -1; float min = float.MaxValue;
            for (int i = 0; i < _tiles.Count; i++)
                if (_tiles[i].Column == column && _tiles[i].Y < min) { min = _tiles[i].Y; idx = i; }

            if (idx < 0) { TriggerFail(); return; } // 點了空行
            _tiles.RemoveAt(idx);
        }

        protected override void Simulate(float dt, bool isFocused)
        {
            if (!isFocused) return; // 背景凍結

            _spawnAcc += dt;
            while (_spawnAcc >= SpawnInterval) { _spawnAcc -= SpawnInterval; Spawn(); }

            for (int i = 0; i < _tiles.Count; i++) _tiles[i].Y -= FallSpeed * dt;

            for (int i = 0; i < _tiles.Count; i++)
                if (_tiles[i].Y <= 0f) { TriggerFail(); return; }
        }

        private void Spawn()
        {
            _tiles.Add(new Tile { Column = _spawnSeq % Columns, Y = Height });
            _spawnSeq++;
        }
    }
}
