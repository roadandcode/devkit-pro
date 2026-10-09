using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoadAndCode.DevKit.Sandbox
{
    /// <summary>Sample data with references nested inside a list of structs.</summary>
    [CreateAssetMenu(fileName = "Waves", menuName = "DevKit Sandbox/Wave Table")]
    public sealed class WaveTable : ScriptableObject
    {
        [Serializable]
        public struct Wave
        {
            [SerializeField] private EnemyDefinition _enemy;
            [SerializeField] private int _count;

            public EnemyDefinition Enemy => _enemy;

            public int Count => _count;
        }

        [SerializeField] private Wave[] _waves = new Wave[0];

        public IReadOnlyList<Wave> Waves => _waves;
    }
}
