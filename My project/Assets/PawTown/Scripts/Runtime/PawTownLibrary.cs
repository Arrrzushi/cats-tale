using System;
using System.Collections.Generic;
using UnityEngine;

namespace PawTown
{
    /// <summary>Direction bits in tile space. N = +Z, E = +X, S = -Z, W = -X.</summary>
    public static class Dir
    {
        public const int N = 1, E = 2, S = 4, W = 8;

        /// <summary>Rotate a 4-bit side mask by k quarter turns clockwise (seen from above), matching a +90 deg Y rotation.</summary>
        public static int Rotate(int mask, int k)
        {
            k = ((k % 4) + 4) % 4;
            for (int i = 0; i < k; i++)
                mask = ((mask << 1) | (mask >> 3)) & 15;
            return mask;
        }

        public static int FromLocal(Vector3 p)
        {
            if (Mathf.Abs(p.x) > Mathf.Abs(p.z)) return p.x > 0 ? E : W;
            return p.z > 0 ? N : S;
        }
    }

    [Serializable]
    public class TileEntry
    {
        public string name;
        public GameObject prefab;
        public int road, rail, river;   // side masks in prefab space (from CONN_* markers)
        public int front;               // FRONT marker side (lots), 0 = none
        public bool isLot;
    }

    [CreateAssetMenu(menuName = "PawTown/Tile Library")]
    public class PawTownLibrary : ScriptableObject
    {
        public float tileSize = 24f;
        public List<TileEntry> tiles = new List<TileEntry>();
        public List<GameObject> backdrop = new List<GameObject>();
        public Material ground;   // far grass plane under the streamed tiles
        public List<GameObject> clouds = new List<GameObject>();

        public TileEntry Get(string n) => tiles.Find(t => t.name == n);
    }
}
