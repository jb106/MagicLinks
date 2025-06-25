using NUnit.Framework;
using MagicLinks.Observables;

namespace MagicLinks.Tests
{
    public class ObservablesTests
    {
        [Test]
        public void MagicEventObservable_RaiseInvokesEvent()
        {
            var observable = new MagicEventObservable<int>();
            int calledValue = 0;
            observable.OnEventRaised += v => calledValue = v;

            observable.Raise(42);

            Assert.AreEqual(42, calledValue);
        }

        [Test]
        public void MagicEventVoidObservable_RaiseInvokesEvent()
        {
            var observable = new MagicEventVoidObservable();
            bool called = false;
            observable.OnEventRaised += () => called = true;

            observable.Raise();

            Assert.IsTrue(called);
        }

        [Test]
        public void MagicVariableObservable_InvokesOnValueChangedOnChange()
        {
            var observable = new MagicVariableObservable<int>();
            int received = 0;
            observable.OnValueChanged += v => received = v;

            observable.Value = 5;

            Assert.AreEqual(5, received);
        }

        [Test]
        public void MagicVariableObservable_DoesNotInvokeWhenValueUnchanged()
        {
            var observable = new MagicVariableObservable<int>();
            int callCount = 0;
            observable.OnValueChanged += _ => callCount++;

            observable.Value = 1;
            observable.Value = 1;

            Assert.AreEqual(1, callCount);
        }

        [Test]
        public void MagicVariableObservable_InitialValueIsSet()
        {
            var observable = new MagicVariableObservable<int>(10);
            Assert.AreEqual(10, observable.Value);
        }

        [Test]
        public void MagicVariableObservable_Vector2InitialValueIsSet()
        {
            var observable = new MagicVariableObservable<UnityEngine.Vector2>(new UnityEngine.Vector2(1f, 2f));
            Assert.AreEqual(new UnityEngine.Vector2(1f, 2f), observable.Value);
        }

        [Test]
        public void MagicVariableObservable_Vector3InitialValueIsSet()
        {
            var observable = new MagicVariableObservable<UnityEngine.Vector3>(new UnityEngine.Vector3(1f, 2f, 3f));
            Assert.AreEqual(new UnityEngine.Vector3(1f, 2f, 3f), observable.Value);
        }
    }
}
