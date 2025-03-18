using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MagicLinks
{
    public class MagicEvent<T> : ScriptableObject, IEvent<T>
    {
        public event Action<T> OnEventRaised;

        [SerializeField] T manualValue;

        [Button]
        private void ManualRaise()
        {
            Raise(manualValue);
        }

        public void Raise(T value)
        {
            OnEventRaised?.Invoke(value);
        }
    }
}