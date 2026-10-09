using System;
using UnityEngine;

namespace RoadAndCode.DevKit.Validation.Tests
{
    /// <summary>A ScriptableObject with every shape of reference field the reader has to cope with.</summary>
    public sealed class ProbeAsset : ScriptableObject
    {
        [Serializable]
        public struct Slot
        {
            [SerializeField] private string _label;
            [SerializeField] private Texture2D _icon;

            public Slot(string label, Texture2D icon)
            {
                _label = label;
                _icon = icon;
            }
        }

        [SerializeField] private Material _assigned;
        [SerializeField] private Material _empty;
        [SerializeField] private Texture2D[] _list = new Texture2D[0];
        [SerializeField] private Slot[] _slots = new Slot[0];
        [SerializeField] private float[] _numbers = new float[0];
        [SerializeField] private string _text;

        public void Fill(Material assigned, Texture2D texture)
        {
            _assigned = assigned;
            _list = new[] { texture, null };
            _slots = new[] { new Slot("first", texture), new Slot("second", null) };
            _numbers = new[] { 1f, 2f, 3f };
        }
    }
}
