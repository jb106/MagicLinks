using UnityEngine;
using UnityEngine.Events;

namespace MagicLinks
{
    public abstract class MagicVariableListener<T> : MonoBehaviour
    {
        [SerializeField] protected UnityEvent<T> _onVariableChanged;

        private void OnEnable()
        {
            Variable.OnValueChanged += OnValueChanged;
            OnValueChanged(Variable.Value);
        }

        private void OnDisable()
        {
            Variable.OnValueChanged -= OnValueChanged;
        }

        protected void OnValueChanged(T v)
        {
            _onVariableChanged.Invoke(v);
        }

        protected abstract MagicVariable<T> Variable { get; }
    }
}
