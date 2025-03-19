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

    public class MagicEvent : ScriptableObject, IEvent
    {
        public event Action OnEventRaised;

        [Button]
        private void ManualRaise()
        {
            Raise();
        }

        public void Raise()
        {
            OnEventRaised?.Invoke();
        }
    }
}