using System.Collections.Generic;
using Newtonsoft.Json;
using NUnit.Framework;
using Structures;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Inventories
{
    public class ConveyedResource : MonoBehaviour, ISavable
    {
        public Materials materialType;
        [SerializeField] private float resourceRadius = 0.2f; // the radius of the resource for collision detection
        [SerializeField] private GameObject model;
        private Vector2Int tile; // the tile the resource is on
        private List<Vector3> pathHalfSegments = new(3); // the 3d points defining the half segments along the path
        private float interpolation; // the interpolation value between the first and last points in the path half segments list, each half segment is 1 unit long in interpolation space
        private Vector2Int exitOrientation; // the direction of the next tile to step into
        private bool isOnBelt = false; // whether the resource is on a conveyor belt or dropped on the ground
        private bool isInInventory = false; // whether the resource is in an inventory or not

        private int _id = -1;
        public int ID
        {
            get
            {
                if (_id == -1) _id = SaveManager.Instance.GenerateUniqueId();
                return _id;
            }
            set => _id = value;
        }

        public string TypeName => GetType().ToString() + materialType.ToString();

        public bool ShouldInstantiateOnLoad() => true;

        public string GetStateJson()
        {
            return JsonConvert.SerializeObject((
                (tile.x, tile.y),
                pathHalfSegments.ConvertAll(point => (point.x, point.y, point.z)),
                interpolation,
                (exitOrientation.x, exitOrientation.y),
                isOnBelt,
                isInInventory
            ));
        }

        public void RestoreStateJson(string stateJson, Dictionary<int, ISavable> idLookup)
        {
            var state = JsonConvert.DeserializeObject<((int, int), List<(float, float, float)>, float, (int, int), bool, bool)>(stateJson);
            tile = new Vector2Int(state.Item1.Item1, state.Item1.Item2);
            pathHalfSegments = state.Item2.ConvertAll(tuple => new Vector3(tuple.Item1, tuple.Item2, tuple.Item3));
            interpolation = state.Item3;
            exitOrientation = new Vector2Int(state.Item4.Item1, state.Item4.Item2);
            isOnBelt = state.Item5;
            isInInventory = state.Item6;
        }

        public void InitializeConveyPath(Vector2Int initialTile, Vector2Int initialOrientation, Vector2Int initialExitOrientation)
        {
            tile = initialTile;
            interpolation = 1;
            NewPath(initialOrientation, initialExitOrientation);
            isOnBelt = true;
        }

        public bool TryEnterConveyPath(Vector2Int tile)
        {
            // check if a conveyor belt has been placed under the resource
            List<ConveyedResource> resourcesOnTile = GameManager.Instance.GetTileResources(tile);
            if (resourcesOnTile == null) return false;

            // check if the resource will overlap with other resources on the center of the tile
            if (IsOverlappingWith(GameManager.TileToVector3(tile), resourcesOnTile)) return false;

            InitializeConveyPath(tile, GameManager.Instance.GetTileOrientation(tile), GameManager.Instance.GetNextConveyorExitOrientation(tile, this));
            GameManager.Instance.ResourceEnterTile(this, tile);
            return true;
        }

        public void ExitConveyPath()
        {
            isOnBelt = false;
            GameManager.Instance.ResourceExitTile(this, tile);
        }

        public void EnterInventory()
        {
            model.SetActive(false);
            isInInventory = true;
        }

        public void ExitInventory()
        {
            model.SetActive(true);
            isInInventory = false;
        }

        public void FixedUpdate()
        {
            if (isOnBelt || isInInventory) return;

            TryEnterConveyPath(GameManager.Vector3ToTile(transform.position));
        }

        public void Convey(float speed, List<ConveyedResource> resourcesOnTile, List<ConveyedResource> resourcesOnNextTile)
        {
            // find the new interpolated position of the resource
            float newInterpolation = interpolation + speed * Time.deltaTime;
            Vector3 newPosition = InterpolatePath(newInterpolation);

            // Check for collisions with other resources on the new position
            if (IsOverlappingWith(newPosition, resourcesOnTile) || IsOverlappingWith(newPosition, resourcesOnNextTile)) return;

            interpolation = newInterpolation;
            // if the resource crosses the end of the path, remove the first tile and add a new one at the end
            if (newInterpolation > 2)
            {
                AdvancePath(exitOrientation);
                transform.position = InterpolatePath(interpolation);
            }
            else
            {
                transform.position = newPosition;
            }
        }

        public bool IsOverlappingWith(Vector3 newPosition, List<ConveyedResource> others)
        {
            if (others == null) return false;
            foreach (ConveyedResource otherResource in others)
            {
                if (otherResource == this) continue;
                if (IsOverlappingWith(newPosition, otherResource)) return true;
            }
            return false;
        }

        public bool IsOverlappingWith(Vector3 newPosition, ConveyedResource other)
        {
            // return Vector3.Distance(newPosition, other.transform.position) < resourceRadius; // L2 distance
            float dist = Mathf.Abs(newPosition.x - other.transform.position.x) + Mathf.Abs(newPosition.z - other.transform.position.z);
            bool result = dist < resourceRadius;
            return result;
        }

        private void AdvancePath(Vector2Int newTileDir)
        {
            Vector2Int newTile = tile + newTileDir;
            if (newTileDir == Vector2Int.zero ||
                GameManager.Instance.GetTileResources(newTile) == null ||
                GameManager.Instance.GetTileOrientation(newTile) != exitOrientation)
            {
                interpolation = 1.99f;
                return;
            }

            // update the path half segments
            exitOrientation = GameManager.Instance.GetNextConveyorExitOrientation(newTile, this);
            Vector2 halfOrientation = new Vector2(exitOrientation.x, exitOrientation.y) * 0.5f;
            Vector3 newPathEnd = new Vector3(newTile.x + halfOrientation.x, 0, newTile.y + halfOrientation.y);
            pathHalfSegments.Add(new Vector3(newTile.x, 0, newTile.y));
            pathHalfSegments.Add(newPathEnd);
            pathHalfSegments.RemoveAt(0);
            pathHalfSegments.RemoveAt(0);

            GameManager.Instance.ResourceEnterTile(this, newTile);
            GameManager.Instance.ResourceExitTile(this, tile);
            tile = newTile;
            interpolation -= 2;
        }

        public void NewPath(Vector2Int newOrientation, Vector2Int newExitOrientation)
        {
            pathHalfSegments.Clear();
            Vector2 halfOrientation = new Vector2(newOrientation.x, newOrientation.y) * 0.5f;
            Vector2 halfExitOrientation = new Vector2(newExitOrientation.x, newExitOrientation.y) * 0.5f;
            pathHalfSegments.Add(new Vector3(tile.x - halfOrientation.x, 0, tile.y - halfOrientation.y));
            pathHalfSegments.Add(new Vector3(tile.x, 0, tile.y));
            pathHalfSegments.Add(new Vector3(tile.x + halfExitOrientation.x, 0, tile.y + halfExitOrientation.y));
            exitOrientation = newExitOrientation;
            transform.position = InterpolatePath(interpolation);
        }

        private Vector3 InterpolatePath(float interpolation)
        {
            int nextSegment = interpolation <= 1 ? 1 : 2;
            int prevSegment = nextSegment - 1;

            // interpolate between the two half segments from 0 to 1 (with overshoot for guessing the next tile path)
            interpolation -= prevSegment;
            return Vector3.LerpUnclamped(pathHalfSegments[prevSegment], pathHalfSegments[nextSegment], interpolation);
        }

        public void DestroyResource()
        {
            if (isOnBelt) ExitConveyPath();
            if (isInInventory) ExitInventory();
            Destroy(gameObject);
        }
    }
}
