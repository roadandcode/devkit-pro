using UnityEngine;

namespace RoadAndCode.DevKit.Sandbox
{
    /// <summary>A component with the kinds of reference a scene usually holds: a prefab, a scene object, an asset.</summary>
    public sealed class Spawner : MonoBehaviour
    {
        [SerializeField] private GameObject _prefab;
        [SerializeField] private Transform _spawnPoint;
        [SerializeField] private WaveTable _waves;

        [Tooltip("Left empty on purpose in the sample scenes: the validator is told that fields named like this may be.")]
        [SerializeField] private Material _optionalTint;

        public WaveTable Waves => _waves;

        public GameObject Spawn()
        {
            if (_prefab == null || _spawnPoint == null) return null;

            GameObject spawned = Instantiate(_prefab, _spawnPoint.position, _spawnPoint.rotation);
            if (_optionalTint != null && spawned.TryGetComponent(out Renderer spawnedRenderer)) spawnedRenderer.sharedMaterial = _optionalTint;
            return spawned;
        }
    }
}
