using UnityEngine;

namespace RoadAndCode.DevKit.Sandbox
{
    /// <summary>Sample data for the tools to look at: something a player can pick up.</summary>
    [CreateAssetMenu(fileName = "Item", menuName = "DevKit Sandbox/Item")]
    public sealed class ItemDefinition : ScriptableObject
    {
        [SerializeField] private string _displayName;
        [SerializeField] private int _price;
        [SerializeField] private Texture2D _icon;
        [SerializeField] private GameObject _worldPrefab;

        public string DisplayName => _displayName;

        public int Price => _price;

        public Texture2D Icon => _icon;

        public GameObject WorldPrefab => _worldPrefab;
    }
}
