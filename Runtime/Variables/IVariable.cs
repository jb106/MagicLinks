using System;

namespace MagicLinks
{
    public interface IVariable<T>
    {
        T InitialValue { get; set; }
        T Value { get; set; }

        event Action<T> OnValueChanged;
    }
}