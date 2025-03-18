using System;

namespace MagicLinks
{
    public interface IEvent<T>
    {
        event Action<T> OnEventRaised;
        void Raise(T value);
    }
}