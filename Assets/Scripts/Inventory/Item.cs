using System;
using UnityEditor;
using UnityEngine;

namespace Inventories
{
    [Serializable]
    public class Item : MonoBehaviour
    {
        public string itemName;
        public Sprite icon;
        public GameObject prefab;
        [HideInInspector] public int[] materialCosts = new int[Enum.GetValues(typeof(Materials)).Length];
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
