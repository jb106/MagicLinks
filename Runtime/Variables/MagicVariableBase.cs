using System;
using UnityEngine;

namespace MagicLinks
{
    public abstract class MagicVariableBase : ScriptableObject
    {
        public Action OnGlobalValueChanged;

        public abstract object GetValueAsObject();

        public abstract Type GetValueType();

        public T GetValue<T>()
        {
            if (GetValueAsObject() is T castedValue)
                return castedValue;

            throw new InvalidCastException($"Impossible de convertir {name} en {typeof(T)}");
        }
    }
}