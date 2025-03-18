using System;
using UnityEngine;
using Sirenix.OdinInspector;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MagicLinks
{
    public class MagicVariable<T> : MagicVariableBase, IVariable<T>
    {
        public event Action<T> OnValueChanged;

        [SerializeField, DisableIf("IsPlaying")] private T initialValue;
        [SerializeField, EnableIf("IsPlaying")] private T value;

        private T inspectorValue;

        public T InitialValue
        {
            get => initialValue;
            set => initialValue = value;
        }

        public T Value
        {
            get => value;
            set
            {
                if (!Equals(this.value, value))
                {
                    this.value = value;
                    OnValueChanged?.Invoke(value);
                    OnGlobalValueChanged?.Invoke();
                }
            }
        }

        private void OnEnable()
        {
            value = initialValue;
            inspectorValue = initialValue;
            OnValueChanged?.Invoke(value);
            OnGlobalValueChanged?.Invoke();
        }

        private bool IsPlaying()
        {
            return Application.isPlaying;
        }

        public override object GetValueAsObject() => value;
        public override Type GetValueType() => typeof(T);

#if UNITY_EDITOR
        private void OnValidate()
        {
            if(!Equals(this.value, inspectorValue))
            {
                inspectorValue = value;
                OnValueChanged?.Invoke(value);
                OnGlobalValueChanged?.Invoke();
            }
        }
#endif
    }
}