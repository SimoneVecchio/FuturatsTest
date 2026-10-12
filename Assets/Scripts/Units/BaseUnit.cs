using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

using FuturatsTest.Data.Units;

using FuturatsTest.Grid;
using FuturatsTest.Grid.Pathfinding;

namespace FuturatsTest.Units
{
    [DisallowMultipleComponent]
    [SelectionBase] // Clicking a model in the Scene view selects the unit
    public class BaseUnit : MonoBehaviour
    {

        [Serializable]
        public class SubUnitOverride
        {
            public Vector2Int cell;
            public GameObject prefab;
        }

        private const string MODELS_NAME = "Models";
        private const float TURN_SNAP_ANGLE = 0.5f;   // degrees to snap the target when rotating

        // Generates models everytime so it's better not save
        private const HideFlags GENERATED_FLAGS = HideFlags.DontSave | HideFlags.NotEditable;

        [Header("Unit Data")]
        [Tooltip("Size, default model and movement. Shared by every unit that uses the same asset.")]
        [SerializeField] private UnitData data;

        [SerializeField, HideInInspector] private List<SubUnitOverride> subUnitOverrides = new List<SubUnitOverride>();

        [Header("Selection")]
        [SerializeField] private Material selectedMaterial;

        private GridManager grid;

        private Vector2Int[] unitTiles = { Vector2Int.zero };
        private Vector2Int centerOffset;
        private Vector2Int pivot;        // pivot tile the unit stands on
        private Vector2Int destination;   // pivot tile the unit will end up on
        private int remainingMovement;
        private bool isSelected;

        // Movement
        private List<Vector2Int> path;
        private List<Vector2Int> pendingPath;   // a new path given in the middle of a step
        private int pathIndex;
        private bool isMoving;
        private bool isStepping;
        private Vector2Int stepTarget;
        private float groundY;

        // Visuals
        private Transform modelsRoot;
        private List<Renderer> modelRenderers = new List<Renderer>();
        private List<Material[]> originalMaterials = new List<Material[]>();
        private List<Transform> subUnitTransforms = new List<Transform>();
        private List<Quaternion> subUnitStartRotations = new List<Quaternion>();
        private Quaternion currentTurn = Quaternion.identity;     // where the models are facing now
        private Quaternion targetTurn = Quaternion.identity;      // where they are turning to


        private void OnValidate()
        {
            RequestModelRebuild();
        }

        private void Awake()
        {
            RebuildUnitTiles();
        }

        private void Start()
        {
            BuildModels();

            if (data == null)
            {
                Debug.LogError(name + ": no Unit Data assigned", this);
                enabled = false;
                return;
            }

            grid = GridManager.Instance;
            if (grid == null)
            {
                Debug.LogError(name + ": no GridManager in the scene", this);
                enabled = false;
                return;
            }

            groundY = transform.position.y;
            pivot = grid.WorldToCell(transform.position);
            destination = pivot;
            ResetMovement();

            if (!grid.GetOccupancy().IsFreeForUnit(this, pivot))
            {
                Debug.LogWarning(name + ": starts on blocked/occupied tiles. Move it onto free tiles", this);
            }

            grid.GetOccupancy().RegisterUnit(this);
            grid.GetOccupancy().SetOccupancy(this, pivot, true);
            transform.position = grid.CellCenter(pivot, groundY); // snap to the map
        }

        private void OnDestroy()
        {
            if (grid != null) grid.GetOccupancy().UnRegisterUnit(this);
        }

        private void Update()
        {
            UpdateTurning();
        }

        public UnitData Data { get => data; }

        public int Width { get => data != null ? data.width : 1; }

        public int Depth { get => data != null ? data.depth : 1; }

        public Vector2Int[] UnitTiles { get => unitTiles; } // Tiles the unit covers, as offsets from its pivot tile

        public Vector2Int CenterOffset { get => centerOffset; } // Used to center the unit on target tile when moving

        public Vector2Int Pivot { get => pivot; }

        public Vector2Int Destination { get => destination; }

        public int RemainingMovement { get => remainingMovement; }

        public bool IsMoving { get => isMoving; }

        public bool IsSelected { get => isSelected; }

        public List<Vector2Int> CurrentPath { get => path; }

        public int PathIndex { get => pathIndex; }


        public void RebuildUnitTiles()
        {
            unitTiles = GetRectangleTiles(Width, Depth);

            // Tile closest to the middle. If the middle is between tiles, bottom one
            centerOffset = new Vector2Int((Width - 1) / 2, (Depth - 1) / 2);
        }

        public static Vector2Int[] GetRectangleTiles(int width, int depth)
        {
            width = Mathf.Max(1, width);
            depth = Mathf.Max(1, depth);

            Vector2Int[] result = new Vector2Int[width * depth];
            int index = 0;
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < depth; y++)
                {
                    result[index] = new Vector2Int(x, y);
                    index++;
                }
            }
            return result;
        }

        public static float GetCellSize()
        {
            if (GridManager.Instance != null) return GridManager.Instance.cellSize;
            return 1f;
        }


        #region Movement
        public void ResetMovement()
        {
            remainingMovement = data.moveSpeed;
        }


        public bool MoveToDestination(Vector2Int goal)
        {
            if (grid == null || isMoving) return false;
            if (!grid.GetOccupancy().IsValidDestination(this, goal)) return false;

            // Every move order gets the full movement points.
            // In the middle of a step, plan from where we are going.
            Vector2Int from = isStepping ? stepTarget : pivot;
            List<Vector2Int> newPath = GridPathfinder.FindPath(grid, this, from, goal, false, data.allowDiagonal, data.moveSpeed);
            if (newPath == null) return false;

            ResetMovement();
            destination = goal;

            if (isStepping)
            {
                pendingPath = newPath;
            }
            else
            {
                path = newPath;
                pathIndex = 1;
            }

            if (!isMoving)
            {
                isMoving = true;
                SetSelected(false);
                StartCoroutine(FollowPath());
            }
            return true;
        }

        private IEnumerator FollowPath()
        {
            float waitTimer = 0f;
            int repathAttempts = 0;

            while (path != null && pathIndex < path.Count)
            {
                Vector2Int nextTile = path[pathIndex];

                // Try to reserve the tiles of this step, or wait and look for detour parth
                if (!grid.GetOccupancy().TryReserveStep(this, pivot, nextTile))
                {
                    waitTimer += Time.deltaTime;
                    if (waitTimer >= data.repathDelay)
                    {
                        waitTimer = 0f;
                        repathAttempts++;

                        bool foundDetour = TryFindDetour();
                        if (!foundDetour && repathAttempts >= data.maxRepathAttempts)
                        {
                            Debug.Log(name + ": path blocked, stopping", this);
                            break;
                        }
                    }
                    yield return null;
                    continue;
                }
                waitTimer = 0f;
                repathAttempts = 0;

                yield return StartCoroutine(StepTo(nextTile));

                if (pendingPath != null)
                {
                    path = pendingPath;
                    pendingPath = null;
                    pathIndex = 1;
                }
            }

            path = null;
            pendingPath = null;
            destination = pivot;
            isMoving = false;
        }


        /// <summary> Looks for a path around whoever is blocking the way </summary>
        private bool TryFindDetour()
        {
            List<Vector2Int> detour = GridPathfinder.FindPath(grid, this, pivot, destination, true, data.allowDiagonal, remainingMovement);
            if (detour == null) return false;

            path = detour;
            pathIndex = 1;
            return true;
        }

        private IEnumerator StepTo(Vector2Int nextTile)
        {
            int stepCost = grid.GetCostForUnit(this, nextTile);
            remainingMovement = Mathf.Max(0, remainingMovement - stepCost);

            isStepping = true;
            stepTarget = nextTile;

            Vector3 startPosition = transform.position;
            Vector3 endPosition = grid.CellCenter(nextTile, groundY);

            // Visuals
            float distance = (nextTile - pivot).magnitude;
            float duration = distance * stepCost / data.animationSpeed;

            FaceDirection(endPosition - startPosition);

            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.deltaTime;
                transform.position = Vector3.Lerp(startPosition, endPosition, Mathf.Clamp01(timer / duration));
                yield return null;
            }
            transform.position = endPosition;

            grid.GetOccupancy().CompleteStep(this, pivot, nextTile);
            pivot = nextTile;
            isStepping = false;
            pathIndex++;
        }

        public Dictionary<Vector2Int, int> FindReachablePositions()
        {
            if (grid == null) return new Dictionary<Vector2Int, int>();

            return GridPathfinder.FindReachablePositions(grid, this, pivot, false, data.allowDiagonal, data.moveSpeed);
        }

        #endregion


        #region Rotation

        private void FaceDirection(Vector3 direction)
        {
            direction.y = 0f;
            if (direction == Vector3.zero) return;

            targetTurn = Quaternion.LookRotation(direction);
        }

        private void UpdateTurning()
        {
            if (modelsRoot == null || currentTurn == targetTurn) return;

            currentTurn = Quaternion.Slerp(currentTurn, targetTurn, data.turnSpeed * Time.deltaTime);
            if (Quaternion.Angle(currentTurn, targetTurn) < TURN_SNAP_ANGLE)
            {
                currentTurn = targetTurn;
            }

            ApplyTurn(currentTurn);
        }

        private void ApplyTurn(Quaternion turn)
        {
            for (int i = 0; i < subUnitTransforms.Count; i++)
            {
                if (subUnitTransforms[i] != null)
                {
                    subUnitTransforms[i].localRotation = turn * subUnitStartRotations[i];
                }
            }
        }

        #endregion

        public GameObject GetSubUnitPrefab(Vector2Int cell)
        {
            SubUnitOverride subUnitOverride = GetSubUnitOverride(cell);
            if (subUnitOverride != null)
            {
                return subUnitOverride.prefab;
            }
            else
            {
                return data != null ? data.defaultModel : null;   // null is default cube
            }
        }

        public bool SubUnitHasOwnPrefab(Vector2Int cell)
        {
            return GetSubUnitOverride(cell) != null;
        }

        public void SetSubUnitPrefab(Vector2Int cell, GameObject prefab)
        {
            subUnitOverrides.RemoveAll(subUnitOverride => subUnitOverride == null || subUnitOverride.cell == cell);
            if (prefab != null)
            {
                subUnitOverrides.Add(new SubUnitOverride { cell = cell, prefab = prefab });
            }
            RequestModelRebuild();
        }

        public void ClearSubUnitPrefabs()
        {
            subUnitOverrides.Clear();
            RequestModelRebuild();
        }

        private SubUnitOverride GetSubUnitOverride(Vector2Int cell)
        {
            foreach (SubUnitOverride subUnitOverride in subUnitOverrides)
            {
                if (subUnitOverride != null && subUnitOverride.cell == cell && subUnitOverride.prefab != null)
                {
                    return subUnitOverride;
                }
            }
            return null;
        }


        public void RequestModelRebuild()
        {
#if UNITY_EDITOR
            if (Application.isPlaying) return;
            EditorApplication.delayCall -= DelayedBuildModels;
            EditorApplication.delayCall += DelayedBuildModels;
#endif
        }

#if UNITY_EDITOR
        private void DelayedBuildModels()
        {
            if (this == null || Application.isPlaying) return;
            if (PrefabUtility.IsPartOfPrefabAsset(this)) return; // the prefab asset itself; its instances build their own
            BuildModels();
        }
#endif

        public void BuildModels()
        {
            DestroyOldModels();

            GameObject modelsObject = new GameObject(MODELS_NAME);
            modelsObject.transform.SetParent(transform, false);
            modelsObject.hideFlags = GENERATED_FLAGS;
            modelsRoot = modelsObject.transform;

            subUnitTransforms.Clear();
            subUnitStartRotations.Clear();

            float cellSize = GetCellSize();
            foreach (Vector2Int cell in GetRectangleTiles(Width, Depth))
            {
                GameObject model = SpawnSubUnit(cell, cellSize);
                subUnitTransforms.Add(model.transform);
                subUnitStartRotations.Add(model.transform.localRotation);
            }

            SaveOriginalMaterials();

            currentTurn = Quaternion.identity;
            targetTurn = Quaternion.identity;
        }

        private GameObject SpawnSubUnit(Vector2Int cell, float cellSize)
        {
            GameObject prefab = GetSubUnitPrefab(cell);

            GameObject model;
            if (prefab != null)
            {
                model = Instantiate(prefab, modelsRoot);
            }
            else
            {
                model = GameObject.CreatePrimitive(PrimitiveType.Cube);
                model.transform.localScale = Vector3.one * cellSize * 0.9f;
            }

            model.name = "SubUnit_" + cell.x + "_" + cell.y;
            model.transform.SetParent(modelsRoot, false);
            model.transform.localPosition = new Vector3(cell.x * cellSize, 0f, cell.y * cellSize);

            // Needed so the unit can be clicked
            if (model.GetComponentInChildren<Collider>() == null)
            {
                model.AddComponent<BoxCollider>();
            }

            SetHideFlags(model.transform, GENERATED_FLAGS);
            return model;
        }

        private void DestroyOldModels()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name != MODELS_NAME) continue;

                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        private static void SetHideFlags(Transform target, HideFlags flags)
        {
            foreach (Transform child in target.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.hideFlags = flags;
            }
        }

        #region Selection

        public bool SetSelected(bool selected)
        {
            if (selected && isMoving) return false;

            isSelected = selected;
            ApplySelectionMaterial(selected);
            return true;
        }

        private void SaveOriginalMaterials()
        {
            modelRenderers.Clear();
            originalMaterials.Clear();

            foreach (Renderer modelRenderer in modelsRoot.GetComponentsInChildren<Renderer>())
            {
                modelRenderers.Add(modelRenderer);
                originalMaterials.Add(modelRenderer.sharedMaterials);
            }
        }

        private void ApplySelectionMaterial(bool selected)
        {
            if (selectedMaterial == null) return;

            for (int i = 0; i < modelRenderers.Count; i++)
            {
                if (modelRenderers[i] == null) continue;

                if (selected)
                {
                    Material[] selectedMaterials = new Material[originalMaterials[i].Length];
                    for (int j = 0; j < selectedMaterials.Length; j++)
                    {
                        selectedMaterials[j] = selectedMaterial;
                    }
                    modelRenderers[i].sharedMaterials = selectedMaterials;
                }
                else
                {
                    modelRenderers[i].sharedMaterials = originalMaterials[i];
                }
            }
        }

        #endregion
    }
}