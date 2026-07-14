using System;
﻿using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Adamantium.Core.Collections
{
    public class TrackingCollection<T> : AdamantiumCollection<T>, INotifyPropertyChanged, INotifyCollectionChanged
    {
        public TrackingCollection()
        {

        }

        public TrackingCollection(IEnumerable<T> values) : base(values)
        { }

        /// <summary>
        /// Fires when collection is changed (items added/removed/replaced or collection was cleared)
        /// </summary>
        public event NotifyCollectionChangedEventHandler CollectionChanged;

        /// <summary>
        /// Fires when Count property changes
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Raises the <see cref="CollectionChanged"/> event with an add action.
        /// </summary>
        /// <param name="items">The items that were added.</param>
        /// <param name="index">The starting index.</param>
        private void NotifyAdd(IList items, int index)
        {
            if (CollectionChanged != null)
            {
                var e = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, items, index);
                CollectionChanged(this, e);
            }

            NotifyCountChanged();
        }

        /// <summary>
        /// Raises the <see cref="CollectionChanged"/> event with an add action.
        /// </summary>
        /// <param name="newItem">The items that were added.</param>
        /// <param name="oldItem">The items that were removed.</param>
        /// <param name="index">The starting index.</param>
        private void NotifyReplace(T oldItem, T newItem, int index)
        {
            if (CollectionChanged != null)
            {
                var e = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, newItem, oldItem, index);
                CollectionChanged(this, e);
            }

            NotifyCountChanged();
        }

        /// <summary>
        /// Raises the <see cref="PropertyChanged"/> event when the <see cref="Count"/> property
        /// changes.
        /// </summary>
        private void NotifyCountChanged()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
        }

        /// <summary>
        /// Raises the <see cref="CollectionChanged"/> event with a remove action.
        /// </summary>
        /// <param name="items">The items that were removed.</param>
        /// <param name="index">The starting index.</param>
        private void NotifyRemove(IList items, int index)
        {
            if (CollectionChanged != null)
            {
                var e = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, items, index);
                CollectionChanged.Invoke(this, e);
            }

            NotifyCountChanged();
        }

        /// <summary>
        /// Raises the <see cref="CollectionChanged"/> event with a reset action.
        /// </summary>
        private void NotifyReset()
        {
            if (CollectionChanged != null)
            {
                var e = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);

                CollectionChanged(this, e);
            }

            NotifyCountChanged();
        }

        protected void NotifyCollectionChanged(NotifyCollectionChangedAction action, IList items)
        {
            var e = new NotifyCollectionChangedEventArgs(action, items);
            CollectionChanged?.Invoke(this, e);
        }

        protected override void OnInsert(int index, T item)
        {
            NotifyAdd(new List<T>() { item }, index);
        }

        protected override void OnRemoveItem(int index, T item)
        {
            NotifyRemove(new List<T>() { item }, index);
        }

        protected override void OnSet(int index, T oldItem, T newItem)
        {
            NotifyReplace(oldItem, newItem, index);
        }

        private List<T> _clearing;   // the items a Clear() is about to drop - kept only while it is being reported

        // A Clear() IS a bulk removal, so it is reported as a Remove, with the items: a downstream mirror needs to know WHAT
        // left to unwind its per-item state (a visual child's parent link, a logical child's Parent), and an itemless Reset
        // cannot convey that. They must be captured HERE, before the array is wiped - by the time the clear is done they are
        // gone.
        //
        // And captured ONLY when somebody is listening: with no subscriber there is nothing to report, and a Clear() of an
        // unobserved collection must not allocate a thing. With one, the copy is not extra - the event carries the list.
        protected override void OnClearing(ArraySegment<T> items)
        {
            if (CollectionChanged == null || items.Count == 0) return;

            _clearing = new List<T>(items.Count);
            foreach (var item in items) _clearing.Add(item);
        }

        protected override void OnCleared()
        {
            if (_clearing == null) return;

            var removed = _clearing;
            _clearing = null;
            NotifyRemove(removed, 0);
        }
    }
}
