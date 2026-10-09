using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace RoadAndCode.DevKit.Sandbox
{
    /// <summary>
    /// Loads sample content in each of the ways the Addressables Audit has to recognise: through an
    /// AssetReference field, by label and by address.
    /// </summary>
    public sealed class ContentLoader : MonoBehaviour
    {
        private const string PropsLabel = "props";
        private const string PillarAddress = "scenery/pillar";
        private const string ArenaAddress = "scenes/arena";

        [SerializeField] private AssetReferenceGameObject _crate;
        [SerializeField] private Transform _crateParent;

        private AsyncOperationHandle<GameObject> _crateHandle;
        private AsyncOperationHandle<GameObject> _pillarHandle;

        public AsyncOperationHandle<System.Collections.Generic.IList<GameObject>> LoadProps()
        {
            return Addressables.LoadAssetsAsync<GameObject>(PropsLabel, null);
        }

        public AsyncOperationHandle<SceneInstance> LoadArena() => Addressables.LoadSceneAsync(ArenaAddress);

        private void Start()
        {
            if (_crate != null && _crate.RuntimeKeyIsValid()) _crateHandle = _crate.InstantiateAsync(_crateParent);
            _pillarHandle = Addressables.InstantiateAsync(PillarAddress, new Vector3(2.5f, 0f, 0f), Quaternion.identity);
        }

        private void OnDestroy()
        {
            if (_crateHandle.IsValid()) Addressables.ReleaseInstance(_crateHandle);
            if (_pillarHandle.IsValid()) Addressables.ReleaseInstance(_pillarHandle);
        }
    }
}
