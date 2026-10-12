using UnityEngine;

namespace FuturatsTest.Data.Units
{

    [CreateAssetMenu(fileName = "New Unit Data", menuName = "FuturatsTest/Units/Unit Data")]
    public class UnitData : ScriptableObject
    {

        [Header("Size")]
        [Tooltip("Tiles along X.")]
        [Min(1)] public int width = 1;
        [Tooltip("Tiles along Z (the direction the model faces).")]
        [Min(1)] public int depth = 1;

        [Header("Model")]
        [Tooltip("Prefab placed on every tile that has no prefab of its own. Empty = cube.")]
        public GameObject defaultModel;

        [Header("Movement")]
        [Min(0)] public int moveSpeed = 4;
        public float animationSpeed = 4f;
        public bool allowDiagonal = true;
        [Tooltip("Seconds to wait when another unit blocks the way before looking for a detour")]
        public float repathDelay = 0.4f;
        public int maxRepathAttempts = 8;
        [Min(0.1f)] public float turnSpeed = 12f;
    }
}