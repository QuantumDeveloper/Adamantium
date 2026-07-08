using System.Collections;
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

        protected override void OnClear(T[] items)
        {
            // Clearing an already-empty collection removed nothing, so raise no notification at all (a bare Reset here
            // still reached handlers - e.g. LogicalChildren, which threw "Reset not supported" - for a no-op change).
            if (items is not { Length: > 0 }) return;

            // A Clear() IS a bulk removal, so report the removed items as a Remove rather than a bare Reset. Downstream
            // mirror collections need to know WHAT left to unwind per-item state - e.g. a control's LogicalChildren must
            // clear each removed child's Parent, which an itemless Reset cannot convey.
            NotifyRemove(items, 0);
        }
    }
}
