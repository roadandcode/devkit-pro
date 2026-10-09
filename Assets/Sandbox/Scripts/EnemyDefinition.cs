using System.Collections.Generic;
using UnityEngine;

namespace RoadAndCode.DevKit.Sandbox
{
    /// <summary>Sample data: an enemy, the prefab it spawns as and what it drops.</summary>
    [CreateAssetMenu(fileName = "Enemy", menuName = "DevKit Sandbox/Enemy")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [SerializeField] private string _displayName;
        [SerializeField] private int _health = 10;
        [SerializeField] private GameObject _prefab;
        [SerializeField] private ItemDefinition[] _drops = new ItemDefinition[0];

        public string DisplayName => _displayName;

        public int Health => _health;

        public GameObject Prefab => _prefab;

        public IReadOnlyList<ItemDefinition> Drops => _drops;
    }
}
