using System;
using UnityEditor;
using UnityEngine;

namespace Inventories
{
    [Serializable]
    public enum BuildableEntityType
    {
        Structure,
        Locomotive,
        Cart
    }

    // TODO low priority: refactor into a definion serializable object and a runtime object to avoid unnecessary multiple monobehaviours
    [Serializable]
    public class BuildableEntity : MonoBehaviour
    {
        public string entityName;
        public Sprite icon;
        public BuildableEntityType type;
        [HideInInspector] public int[] resourceCosts = new int[Enum.GetValues(typeof(ResourceType)).Length];

        public bool Place(Vector3 position, Vector2Int placementOrientation, Collider collider)
        {
            if (!GameManager.Instance.HasResources(resourceCosts)) return false;

            if (type == BuildableEntityType.Structure)
            {
                Vector2Int tile = GameManager.Vector3ToTile(position);
                if (!GameManager.Instance.AddStructure(tile, placementOrientation, gameObject)) return false;
            }
            else if (type == BuildableEntityType.Locomotive)
            {
                Vector2Int tile = GameManager.Vector3ToTile(position);
                if (!GameManager.Instance.BuildTrain(tile)) return false;
            }
            else if (type == BuildableEntityType.Cart)
            {
                Train train = collider.GetComponentInParent<Train>();
                if (train == null || !train.AddCart(gameObject)) return false;
            }

            GameManager.Instance.SpendResources(resourceCosts);
            return true;
        }

        public void Destroy()
        {
            GameManager.Instance.AddResources(resourceCosts);
            Destroy(gameObject);
        }
    }

    [CustomEditor(typeof(BuildableEntity))]
    public class BuildableEntityEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            BuildableEntity buildableEntity = (BuildableEntity)target;

            // Draw the default inspector
            DrawDefaultInspector();

            // Custom display for resourceCosts array
            EditorGUILayout.Space();
            if (buildableEntity.resourceCosts != null && buildableEntity.resourceCosts.Length > 0)
            {
                EditorGUILayout.LabelField("Resource Costs", EditorStyles.boldLabel);
                for (int i = 0; i < buildableEntity.resourceCosts.Length; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    buildableEntity.resourceCosts[i] = EditorGUILayout.IntField(((ResourceType)i).ToString(), buildableEntity.resourceCosts[i]);
                    EditorGUILayout.EndHorizontal();
                }
            }
        }
    }
}
