using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using MagicLinks.Network;
using MagicLinks.Observables;

namespace MagicLinks.Tests
{
    public class NetworkSyncTests
    {
        private class FakeVariableSync<T> : IMagicVariableSync<T>
        {
            public bool IsActive { get; set; } = true;
            public readonly List<T> Requests = new List<T>();
            public TaskCompletionSource<MagicWriteResult> Pending = new TaskCompletionSource<MagicWriteResult>();

            public void RequestSet(T value) => Requests.Add(value);

            public Task<MagicWriteResult> RequestSetAsync(T value)
            {
                Requests.Add(value);
                return Pending.Task;
            }

            public Task<MagicWriteResult> RequestSetIfAsync(T expected, T value)
            {
                Requests.Add(value);
                return Pending.Task;
            }
        }

        private class FakeListSync<T> : IMagicListSync<T>
        {
            public bool IsActive { get; set; } = true;
            public readonly List<string> Requests = new List<string>();

            public Task<MagicWriteResult> Record(string request)
            {
                Requests.Add(request);
                return Task.FromResult(MagicWriteResult.Accepted);
            }

            public Task<MagicWriteResult> RequestAdd(T item, bool wantResult) => Record($"Add {item}");
            public Task<MagicWriteResult> RequestRemoveAt(int index, T expected, bool wantResult) => Record($"RemoveAt {index} {expected}");
            public Task<MagicWriteResult> RequestSetAt(int index, T expected, T item, bool wantResult) => Record($"SetAt {index} {expected} {item}");
            public Task<MagicWriteResult> RequestModifyAt(int index, T current, System.Action<T> update, bool wantResult) => Record($"ModifyAt {index}");
            public Task<MagicWriteResult> RequestClear(bool wantResult) => Record("Clear");
        }

        private class FakeEventSync<T> : IMagicEventSync<T>
        {
            public bool IsActive { get; set; } = true;
            public readonly List<T> Raised = new List<T>();
            public void RequestRaise(T value) => Raised.Add(value);
        }

        // --- Offline: strictly the same behaviour as before

        [Test]
        public void SetAsync_Offline_CommitsImmediately()
        {
            var observable = new MagicVariableObservable<int>();

            Task<MagicWriteResult> task = observable.SetAsync(3);

            Assert.IsTrue(task.IsCompleted);
            Assert.AreEqual(MagicWriteResult.Accepted, task.Result);
            Assert.AreEqual(3, observable.Value);
        }

        [Test]
        public void SetIfAsync_Offline_SucceedsWhenExpectedMatches()
        {
            var observable = new MagicVariableObservable<int>(-1);

            Task<MagicWriteResult> task = observable.SetIfAsync(-1, 7);

            Assert.AreEqual(MagicWriteResult.Accepted, task.Result);
            Assert.AreEqual(7, observable.Value);
        }

        [Test]
        public void SetIfAsync_Offline_RefusedWhenExpectedDiffers()
        {
            var observable = new MagicVariableObservable<int>(2);
            int changes = 0;
            observable.OnValueChanged += _ => changes++;

            Task<MagicWriteResult> task = observable.SetIfAsync(-1, 7);

            Assert.AreEqual(MagicWriteResult.Refused, task.Result);
            Assert.AreEqual(2, observable.Value);
            Assert.AreEqual(0, changes);
        }

        [Test]
        public void InactiveSync_BehavesAsLocal()
        {
            var observable = new MagicVariableObservable<int>();
            var sync = new FakeVariableSync<int> { IsActive = false };
            observable.Sync = sync;

            observable.Value = 5;

            Assert.AreEqual(5, observable.Value);
            Assert.IsEmpty(sync.Requests);
        }

        // --- In session: writes are requests, the value only changes when the authority commits it

        [Test]
        public void ActiveSync_SetValue_SendsRequestWithoutChangingLocalValue()
        {
            var observable = new MagicVariableObservable<int>(1);
            var sync = new FakeVariableSync<int>();
            observable.Sync = sync;
            int changes = 0;
            observable.OnValueChanged += _ => changes++;

            observable.Value = 5;

            Assert.AreEqual(1, observable.Value);
            Assert.AreEqual(0, changes);
            CollectionAssert.AreEqual(new[] { 5 }, sync.Requests);
        }

        [Test]
        public void ActiveSync_CommitFromAuthority_NotifiesListeners()
        {
            var observable = new MagicVariableObservable<int>(1);
            observable.Sync = new FakeVariableSync<int>();
            int received = 0;
            observable.OnValueChanged += v => received = v;

            observable.Commit(9);

            Assert.AreEqual(9, observable.Value);
            Assert.AreEqual(9, received);
        }

        [Test]
        public void ActiveSync_SetIfAsync_WaitsForTheAuthority()
        {
            var observable = new MagicVariableObservable<int>(-1);
            var sync = new FakeVariableSync<int>();
            observable.Sync = sync;

            Task<MagicWriteResult> task = observable.SetIfAsync(-1, 4);

            Assert.IsFalse(task.IsCompleted);
            sync.Pending.SetResult(MagicWriteResult.Disconnected);
            Assert.AreEqual(MagicWriteResult.Disconnected, task.Result);
        }

        [Test]
        public void ActiveSync_ListOperations_AreSentAsRequests()
        {
            var list = new MagicListVariableObservable<int>();
            list.Add(1);
            list.Add(2);

            var sync = new FakeListSync<int>();
            list.Sync = sync;

            list.Add(3);
            list.Remove(1);
            list.SetAt(1, 5);
            list.Clear();

            CollectionAssert.AreEqual(new[] { 1, 2 }, list.Value);
            CollectionAssert.AreEqual(new[] { "Add 3", "RemoveAt 0 1", "SetAt 1 2 5", "Clear" }, sync.Requests);
        }

        [Test]
        public void ActiveSync_ListRemoveOfMissingItem_ReturnsFalseAndSendsNothing()
        {
            var list = new MagicListVariableObservable<int>();
            var sync = new FakeListSync<int>();
            list.Sync = sync;

            Assert.IsFalse(list.Remove(42));
            Assert.IsEmpty(sync.Requests);
        }

        [Test]
        public void List_AppliedChanges_RaiseTheSameEventsAsOffline()
        {
            var list = new MagicListVariableObservable<string>();
            list.Sync = new FakeListSync<string>();
            var events = new List<string>();
            list.OnItemAdded += i => events.Add($"added {i}");
            list.OnItemRemoved += i => events.Add($"removed {i}");

            list.AddLocal("a");
            list.RemoveAtLocal(0);

            CollectionAssert.AreEqual(new[] { "added a", "removed a" }, events);
        }

        // --- Write rules (offline, they run locally)

        [Test]
        public void VariableWriteRule_Offline_RefusesWrites()
        {
            var observable = new MagicVariableObservable<int>(1);
            observable.WriteRule = (fromClientId, current, value) => value >= 0;

            observable.Value = -5;
            Task<MagicWriteResult> task = observable.SetAsync(-1);

            Assert.AreEqual(1, observable.Value);
            Assert.AreEqual(MagicWriteResult.Refused, task.Result);
            Assert.AreEqual(MagicWriteResult.Accepted, observable.SetAsync(3).Result);
            Assert.AreEqual(3, observable.Value);
        }

        [Test]
        public void ListWriteRule_Offline_SeesTheChangeAndTheCurrentItems()
        {
            var interfaces = new MagicListVariableObservable<string>();
            interfaces.Add("Radio");
            interfaces.Add("");

            // Nobody can open an interface another entry already has open
            interfaces.WriteRule = (fromClientId, change, items) =>
                change.Operation != MagicListOperation.SetAt
                || string.IsNullOrEmpty(change.Item)
                || items.Where((item, i) => i != change.Index).All(item => item != change.Item);

            Assert.AreEqual(MagicWriteResult.Refused, interfaces.SetAtAsync(1, "Radio").Result);
            Assert.AreEqual(MagicWriteResult.Accepted, interfaces.SetAtAsync(1, "Nav").Result);
            CollectionAssert.AreEqual(new[] { "Radio", "Nav" }, interfaces.Value);
        }

        [Test]
        public void ListAsyncOperations_Offline_CompleteImmediately()
        {
            var list = new MagicListVariableObservable<int>();

            Assert.AreEqual(MagicWriteResult.Accepted, list.AddAsync(4).Result);
            Assert.AreEqual(MagicWriteResult.Refused, list.RemoveAsync(99).Result);
            Assert.AreEqual(0, list.FindIndex(i => i == 4));
            Assert.AreEqual(MagicWriteResult.Accepted, list.RemoveAtAsync(0).Result);
            Assert.IsEmpty(list.Value);
        }

        [Test]
        public void ActiveSync_Raise_IsNotRaisedLocallyBeforeTheEcho()
        {
            var observable = new MagicEventObservable<string>();
            var sync = new FakeEventSync<string>();
            observable.Sync = sync;
            int raised = 0;
            observable.OnEventRaised += _ => raised++;

            observable.Raise("hello");

            Assert.AreEqual(0, raised);
            CollectionAssert.AreEqual(new[] { "hello" }, sync.Raised);

            observable.RaiseLocal("hello");
            Assert.AreEqual(1, raised);
        }
    }
}
