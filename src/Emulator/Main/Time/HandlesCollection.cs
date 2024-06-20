//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace Antmicro.Renode.Time
{
    /// <summary>
    /// Represents a collection of time handles and allows to iterate over it in an optimal way.
    /// </summary>
    /// <remarks>
    /// It keeps track of handles not ready for a new time grant in a separate list so they can be accessed faster.
    /// If there are any not ready time handles it will iterate only over them.
    /// </remarks>
    public sealed class HandlesCollection : IEnumerable<TimeHandle>
    {
        /// <summary>
        /// Creates new empty collection.
        /// </summary>
        public HandlesCollection()
        {
            ready = new LinkedList<TimeHandle>();
            notReady = new LinkedList<TimeHandle>();
            locker = new object();
        }

        /// <summary>
        /// Executes `Latch` method on all handles and removes the disposed ones from the collection.
        /// </summary>
        public void LatchAllAndCollectGarbage()
        {
            var wasLocked = false;
            try
            {
                InnerLatchAndCollectGarbage(ref wasLocked, notReady);
                InnerLatchAndCollectGarbage(ref wasLocked, ready);
            }
            finally
            {
                if(wasLocked)
                {
                    Monitor.Exit(locker);
                }
            }
        }

        /// <summary>
        /// Executes `Unlatch` method on all handles.
        /// </summary>
        public void UnlatchAll()
        {
            foreach(var h in All)
            {
                h.Unlatch();
            }

            // After unlatch some handles might be disabled.
            // Disabling not ready handles make them ready.
            // We need to update list to reflect changes.
            var currentNode = notReady.First;
            while(currentNode != null)
            {
                var next = currentNode.Next;
                UpdateHandle(currentNode);
                currentNode = next;
            }
        }

        /// <summary>
        /// Adds new handle to the collection.
        /// </summary>
        /// <remarks>
        /// Depending of the value of <see cref="IsReadyForNewTimeGrant">, the handle is put either in <see cref="ready"> or <see cref="notReady"> queue.
        /// </remarks>
        public void Add(TimeHandle handle)
        {
            lock(locker)
            {
                (handle.IsReadyForNewTimeGrant ? ready : notReady).AddLast(handle);
                handle.IsDone = handle.IsReadyForNewTimeGrant;
            }
        }

        /// <summary>
        /// Updates state of a the handle by moving it to a proper queue.
        /// </summary>
        public void UpdateHandle(LinkedListNode<TimeHandle> handle)
        {
            var isOnReadyList = handle.List == ready;
            if((handle.Value.IsReadyForNewTimeGrant && isOnReadyList)
               || (!handle.Value.IsReadyForNewTimeGrant && !isOnReadyList))
            {
                return;
            }

            lock(locker)
            {
                if(isOnReadyList)
                {
                    ready.Remove(handle);
                    notReady.AddLast(handle);
                    handle.Value.IsDone = false;
                }
                else
                {
                    notReady.Remove(handle);
                    ready.AddLast(handle);
                    handle.Value.IsDone = true;
                }
            }
        }

        /// <summary>
        /// Returns an enumerator over the part of handles collection - either not-ready-for-a-new-time-grant handles (if any) or ready ones (otherwise).
        /// </summary>
        public IEnumerator<TimeHandle> GetEnumerator()
        {
            return WithLinkedListNode.Select(x => x.Value).GetEnumerator();
        }

        /// <summary>
        /// Calculates a base elapsed virtual time (minimal) for all handles.
        /// Returns `false` if the handles collection is empty.
        /// </summary>
        public bool TryGetCommonElapsedTime(out TimeInterval commonElapsedTime)
        {
            lock(locker)
            {
                if(notReady.Count == 0 && ready.Count == 0)
                {
                    commonElapsedTime = TimeInterval.Empty;
                    return false;
                }
                commonElapsedTime = All.Min(x => x.TotalElapsedTime);
                return true;
            }
        }

        /// <summary>
        /// Gets an enumerator over the part of handles collection - either not-ready-for-a-new-time-grant handles (if any) or ready ones (otherwise) returning objects of <see cref="LinkedListNode{TimeHandle}"/>.
        /// </summary>
        public IEnumerable<LinkedListNode<TimeHandle>> WithLinkedListNode
        {
            get
            {
                var currentNode = (AreAllReadyForNewGrant ? ready : notReady).First;
                while(currentNode != null)
                {
                    var next = currentNode.Next;
                    yield return currentNode;
                    currentNode = next;
                }
            }
        }

        /// <summary>
        /// Returns true if all handles in collection are in ready-for-a-new-grant state.
        /// </summary>
        public bool AreAllReadyForNewGrant { get { return notReady.Count == 0; } }

        /// <summary>
        /// Returns the number of handles that would be enumerated by <see cref="GetEnumerator">, i.e., either not-ready-for-a-new-grant (if any) or ready-for-a-new-grant (otherwise).
        /// </summary>
        public int ActiveCount { get { return AreAllReadyForNewGrant ? ready.Count : notReady.Count; } }

        /// <summary>
        /// Returns an enumeration of all handles in the collection - not-ready queue concatenated with ready one.
        /// </summary>
        public IEnumerable<TimeHandle> All { get { return notReady.Concat(ready); } }

        /// <see cref="GetEnumerator">
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        private void InnerLatchAndCollectGarbage(ref bool wasLocked, LinkedList<TimeHandle> list)
        {
            var current = list.First;
            while(current != null)
            {
                var next = current.Next;
                current.Value.Latch();

                if(current.Value.DetachRequested)
                {
                    if(!wasLocked)
                    {
                        Monitor.Enter(locker, ref wasLocked);
                    }
                    list.Remove(current);
                    current.Value.Unlatch();
                }

                current = next;
            }
        }

        private readonly LinkedList<TimeHandle> ready;
        private readonly LinkedList<TimeHandle> notReady;
        private readonly object locker;
    }
}
