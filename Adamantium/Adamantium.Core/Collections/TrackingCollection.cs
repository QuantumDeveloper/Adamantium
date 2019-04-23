using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Adamantium.Engine.Core.Collections
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
                CollectionChanged(this, e);
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

        protected override void OnClear()
        {
            NotifyReset();
        }
    }
}
