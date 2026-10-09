using UnityEngine;

namespace RoadAndCode.DevKit.Sandbox
{
    /// <summary>Turns its object, so the sandbox's web build shows something moving.</summary>
    public sealed class Spinner : MonoBehaviour
    {
        [SerializeField] private float _degreesPerSecond = 45f;

        private void Update() => transform.Rotate(0f, _degreesPerSecond * Time.deltaTime, 0f, Space.World);
    }
}
