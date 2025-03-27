using System;
using UnityEditor;
using UnityEngine;

namespace Inventories
{
    [Serializable]
    public enum ItemType
    {
        Structure,
        Locomotive,
        Cart
    }

    [Serializable]
    public class Item : MonoBehaviour
    {
        public string itemName;
        public Sprite icon;
        public ItemType type;
        [HideInInspector] public int[] materialCosts = new int[Enum.GetValues(typeof(Materials)).Length];

        public bool Place(Vector3 position, Vector2Int placementOrientation, Collider collider)
        {
            if (!GameManager.Instance.HasMaterials(materialCosts)) return false;

            if (type == ItemType.Structure)
            {
                Vector2Int tile = Vector2Int.RoundToInt(new Vector2(position.x, position.z));
                if (!GameManager.Instance.AddStructure(tile, placementOrientation, gameObject)) return false;
            }
            else if (type == ItemType.Locomotive)
            {
                Vector2Int tile = Vector2Int.RoundToInt(new Vector2(position.x, position.z));
                if (!GameManager.Instance.BuildTrain(tile)) return false;
            }
            else if (type == ItemType.Cart)
            {
                Train train = collider.GetComponentInParent<Train>();
                if (train == null || !train.AddCart(gameObject)) return false;
            }

            GameManager.Instance.SpendMaterials(materialCosts);
            return true;
        }

        public void Destroy()
        {
            GameManager.Instance.AddMaterials(materialCosts);
            Destroy(gameObject);
        }
    }

    [CustomEditor(typeof(Item))]
    public class ItemEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            Item item = (Item)target;

            // Draw the default inspector
            DrawDefaultInspector();

            // Custom display for materialCosts array
            EditorGUILayout.Space();
            if (item.materialCosts != null && item.materialCosts.Length > 0)
            {
                EditorGUILayout.LabelField("Material Costs", EditorStyles.boldLabel);
                for (int i = 0; i < item.materialCosts.Length; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    item.materialCosts[i] = EditorGUILayout.IntField(((Materials)i).ToString(), item.materialCosts[i]);
                    EditorGUILayout.EndHorizontal();
                }
            }
        }
    }
}
