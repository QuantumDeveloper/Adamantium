using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace Adamantium.Core.Collections
{
    /// <summary>
    /// Observable collection, which could be used for binding 
    /// </summary>
    /// <remarks>Implements <see cref="INotifyPropertyChanged"/> and <see cref="INotifyPropertyChanged"/> interfaces</remarks>
    /// <typeparam name="T"></typeparam>
    public class AdamantiumCollection<T> : IList<T>, IReadOnlyCollection<T>
    {
        private T[] items;
        private int currentIndex = 0;
        private readonly int defaultCapacity = 5;
        private readonly object syncObject = new object();

        public int Capacity
        {
            get { return items.Length; }
            set
            {
                if (value != items.Length)
                {
                    if (value > 0)
                    {
                        var dst = new T[value];
                        if (currentIndex > 0)
                        {
                            Array.Copy(items, 0, dst, 0, currentIndex);
                        }
                        items = dst;
                    }
                    else
                    {
                        items = new T[0];
                    }
                }
            }
        }

        /// <summary>
        /// Constructs <see cref="AdamantiumCollection{T}"/>
        /// </summary>
        public AdamantiumCollection()
        {
            items = new T[0];
        }

        /// <summary>
        /// Constructs <see cref="AdamantiumCollection{T}"/> using capacity
        /// </summary>
        /// <param name="capacity"></param>
        public AdamantiumCollection(Int32 capacity)
        {
            IsFixedSize = true;
            items = new T[capacity];
        }

        /// <summary>
        /// Constructs <see cref="AdamantiumCollection{T}"/> using another IEnumerable collection
        /// </summary>
        /// <param name="items"></param>
        public AdamantiumCollection(IEnumerable<T> items)
        {
            AddRange(items);
        }

        /// <summary>
        /// Constructs <see cref="AdamantiumCollection{T}"/> using params T[] collection (without need to create array)
        /// </summary>
        /// <param name="items"></param>
        public AdamantiumCollection(params T[] items)
        {
            AddRange(items);
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>
        /// An enumerator that can be used to iterate through the collection.
        /// </returns>
        /// <filterpriority>1</filterpriority>
        public IEnumerator<T> GetEnumerator()
        {
            lock (SyncRoot)
            {
                return new Enumerator(this);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            lock (SyncRoot)
            {
                return GetEnumerator();
            }
        }

        /// <summary>
        /// Adds an item to the <see cref="AdamantiumCollection{T}"/>.
        /// </summary>
        /// <param name="item">The object to add to the <see cref="AdamantiumCollection{T}"/>.</param>
        public void Add(T item)
        {
            Insert(currentIndex, item);
        }


        /// <summary>
        /// Returns <see cref="ReadOnlyCollection{T}"/> of the <see cref="AdamantiumCollection{T}"/>
        /// </summary>
        /// <returns></returns>
        public ReadOnlyCollection<T> AsReadOnly()
        {
            lock (SyncRoot)
            {
                return new ReadOnlyCollection<T>(this);
            }
        }

        /// <summary>
        /// Removes all items from the <see cref="AdamantiumCollection{T}"/>.
        /// </summary>
        public void Clear()
        {
            lock (SyncRoot)
            {
                if (currentIndex > 0)
                {
                    ClearItems();
                    OnClear();
                }
            }
        }

        /// <summary>
        /// Clears items array and set Capacity to 0
        /// </summary>
        protected virtual void ClearItems()
        {
            Array.Clear(items, 0, currentIndex);
            Capacity = 0;
            currentIndex = 0;
        }

        /// <summary>
        /// Inserts the range in the <see cref="AdamantiumCollection{T}"/>
        /// </summary>
        /// <param name="index">The zero-based index at which item should be inserted.</param>
        /// <param name="range"><see cref="IEnumerable{T}"/> of items to insert</param>
        public void InsertRange(int index, IEnumerable<T> range)
        {
            lock (SyncRoot)
            {
                var enumerable = range as T[] ?? range.ToArray();
                for (var i = 0; i < enumerable.Length; i++)
                {
                    var element = enumerable[i];
                    Insert(index + i, element);
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the <see cref="T:System.Collections.IList"/> has a fixed size.
        /// </summary>
        /// <returns>
        /// true if the <see cref="T:System.Collections.IList"/> has a fixed size; otherwise, false.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public bool IsFixedSize { get; }

        /// <summary>
        /// Gets or sets a validation routine that can be used to validate items before they are
        /// added.
        /// </summary>
        //public Action<T> Validate { get; set; }

        /// <summary>
        /// Determines whether the <see cref="AdamantiumCollection{T}"/> contains a specific value.
        /// </summary>
        /// <returns>
        /// true if <paramref name="item"/> is found in the <see cref="AdamantiumCollection{T}"/>; otherwise, false.
        /// </returns>
        /// <param name="item">The object to locate in the <see cref="AdamantiumCollection{T}"/>.</param>
        public bool Contains(T item)
        {
            lock (SyncRoot)
            {
                for (int i = 0; i < currentIndex; ++i)
                {
                    if (EqualityComparer<T>.Default.Equals(items[i], item))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// Copies the elements of the <see cref="T:System.Collections.Generic.ICollection`1"/> to an <see cref="T:System.Array"/>, starting at a particular <see cref="T:System.Array"/> index.
        /// </summary>
        /// <param name="array">The one-dimensional <see cref="T:System.Array"/> that is the destination of the elements copied from <see cref="T:System.Collections.Generic.ICollection`1"/>. The <see cref="T:System.Array"/> must have zero-based indexing.</param><param name="arrayIndex">The zero-based index in <paramref name="array"/> at which copying begins.</param><exception cref="T:System.ArgumentNullException"><paramref name="array"/> is null.</exception><exception cref="T:System.ArgumentOutOfRangeException"><paramref name="arrayIndex"/> is less than 0.</exception><exception cref="T:System.ArgumentException">The number of elements in the source <see cref="T:System.Collections.Generic.ICollection`1"/> is greater than the available space from <paramref name="arrayIndex"/> to the end of the destination <paramref name="array"/>.</exception>
        public void CopyTo(T[] array, int arrayIndex)
        {
            lock (SyncRoot)
            {
                items.CopyTo(array, arrayIndex);
            }
        }

        /// <summary>
        /// Removes the first occurrence of a specific object from the <see cref="AdamantiumCollection{T}"/>.
        /// </summary>
        /// <returns>
        /// true if <paramref name="item"/> was successfully removed from the <see cref="AdamantiumCollection{T}"/>; otherwise, false. This method also returns false if <paramref name="item"/> is not found in the original <see cref="T:System.Collections.Generic.ICollection`1"/>.
        /// </returns>
        /// <param name="item">The object to remove from the <see cref="AdamantiumCollection{T}"/>.</param>
        public bool Remove(T item)
        {
            lock (SyncRoot)
            {
                int index = Array.IndexOf(items, item);

                if (index != -1)
                {
                    RemoveInternal(index);
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Removes a set of items defined in <see cref="IEnumerable{T}"/> from <see cref="AdamantiumCollection{T}"/>
        /// </summary>
        /// <param name="elements"><see cref="IEnumerable{T}"/> containing items to remove</param>
        public void Remove(IEnumerable<T> elements)
        {
            lock (SyncRoot)
            {
                foreach (var item in elements)
                {
                    Remove(item);
                }
            }
        }

        /// <summary>
        /// Removes a range of elements from the collection.
        /// </summary>
        /// <param name="index">The first index to remove.</param>
        /// <param name="count">The number of items to remove.</param>
        public virtual void RemoveRange(int index, int count)
        {
            lock (SyncRoot)
            {
                if (index <= currentIndex && count > 0)
                {
                    for (int i = index; i < index + count; i++)
                    {
                        RemoveInternal(i);
                    }
                }
            }
        }

        /// <summary>
        /// Adds all items from <see cref="IEnumerable{T}"/> to <see cref="AdamantiumCollection{T}"/>
        /// </summary>
        /// <param name="range"><see cref="IEnumerable{T}"/> of items which should be added to the Collection</param>
        /// <exception cref="ArgumentNullException"></exception>
        public void AddRange(IEnumerable<T> range)
        {
            if (range == null)
            {
                throw new ArgumentNullException(nameof(range));
            }

            lock (SyncRoot)
            {
                var itemsToAdd = range.ToArray();
                for (int i = 0; i < itemsToAdd.Length; ++i)
                {
                    Insert(currentIndex, itemsToAdd[i]);
                }
            }
        }

        /// <summary>
        /// Gets the number of elements contained in the <see cref="AdamantiumCollection{T}"/>.
        /// </summary>
        /// <returns>
        /// The number of elements contained in the <see cref="AdamantiumCollection{T}"/>.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public int Count => currentIndex;

        /// <summary>
        /// Gets an object that can be used to synchronize access to the <see cref="T:System.Collections.ICollection"/>.
        /// </summary>
        /// <returns>
        /// An object that can be used to synchronize access to the <see cref="T:System.Collections.ICollection"/>.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public object SyncRoot => syncObject;

        /// <summary>
        /// Gets a value indicating whether access to the <see cref="AdamantiumCollection{T}"/> is synchronized (thread safe).
        /// </summary>
        /// <returns>
        /// true if access to the <see cref="AdamantiumCollection{T}"/> is synchronized (thread safe); otherwise, false.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public bool IsSynchronized => true;

        public bool IsReadOnly => false;

        /// <summary>
        /// Determines the index of a specific item in the <see cref="AdamantiumCollection{T}"/>.
        /// </summary>
        /// <returns>
        /// The index of <paramref name="item"/> if found in the list; otherwise, -1.
        /// </returns>
        /// <param name="item">The object to locate in the <see cref="AdamantiumCollection{T}"/>.</param>
        public int IndexOf(T item)
        {
            lock (SyncRoot)
            {
                return Array.IndexOf(items, item);
            }
        }

        /// <summary>
        /// Inserts an item to the <see cref="AdamantiumCollection{T}"/> at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index at which <paramref name="item"/> should be inserted.</param>
        /// <param name="item">The object to insert into the <see cref="AdamantiumCollection{T}"/>. </param>
        /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index"/> is not a valid index in the <see cref="T:System.Collections.Generic.IList`1"/>.</exception>
        /// <exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.IList`1"/> is read-only.</exception>
        public void Insert(int index, T item)
        {
            lock (SyncRoot)
            {
                InsertItem(index, item);
                OnInsert(index, item);
            }
        }

        private void CheckCapacity(int size)
        {
            if (Capacity == size)
            {
                var count = (items.Length + 1) * 2;
                if (count < size)
                {
                    count = size;
                }
                Capacity = count;
            }
        }

        protected virtual void OnInsert(int index, T item) { }

        protected virtual void OnSet(int index, T oldItem, T newItem) { }

        protected virtual void OnRemoveItem(int index, T item) { }

        protected virtual void OnClear() { }

        /// <summary>
        /// Removes the item at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the item to remove.</param><exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index"/> is not a valid index in the <see cref="T:System.Collections.Generic.IList`1"/>.</exception><exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.IList`1"/> is read-only.</exception>
        public void RemoveAt(int index)
        {
            RemoveInternal(index);
        }

        protected virtual void InsertItem(int index, T item)
        {
            CheckCapacity(currentIndex);
            if (index < currentIndex)
            {
                Array.Copy(items, index, items, index + 1, currentIndex - index);
            }
            items[currentIndex] = item;
            currentIndex++;
        }

        private void RemoveInternal(int index)
        {
            lock (SyncRoot)
            {
                T item = RemoveItem(index);
                OnRemoveItem(index, item);
            }
        }

        protected virtual T RemoveItem(int index)
        {
            currentIndex--;
            T item = items[index];
            if (index < currentIndex)
            {
                Array.Copy(items, index + 1, items, index, currentIndex - index);
            }
            items[currentIndex] = default(T);

            if (currentIndex == 0)
            {
                Capacity = defaultCapacity;
            }
            else if (items.Length / currentIndex >= 2)
            {
                Capacity = currentIndex;
            }

            return item;
        }

        protected virtual void SetItem(int index, T item)
        {
            items[index] = item;
        }

        /// <summary>
        /// Gets or sets the element at the specified index.
        /// </summary>
        /// <returns>
        /// The element at the specified index.
        /// </returns>
        /// <param name="index">The zero-based index of the element to get or set.</param><exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index"/> is not a valid index in the <see cref="T:System.Collections.Generic.IList`1"/>.</exception><exception cref="T:System.NotSupportedException">The property is set and the <see cref="T:System.Collections.Generic.IList`1"/> is read-only.</exception>
        public T this[int index]
        {
            get
            {
                lock (SyncRoot)
                {
                    return items[index];
                }
            }
            set
            {
                lock (SyncRoot)
                {
                    T oldItem = items[index];
                    SetItem(index, value);
                    OnSet(index, oldItem, value);
                }
            }
        }

        /// <summary>
        /// Returns an array representation of <see cref="AdamantiumCollection{T}"/>
        /// </summary>
        /// <returns></returns>
        public T[] ToArray()
        {
            lock (SyncRoot)
            {
                T[] array = new T[currentIndex];
                Array.Copy(items, array, currentIndex);
                return array;
            }
        }

        public struct Enumerator : IEnumerator<T>
        {
            private readonly AdamantiumCollection<T> collection;

            private int index;

            private T current;

            internal Enumerator(AdamantiumCollection<T> collection)
            {
                this.collection = collection;
                index = 0;
                current = default(T);
            }

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                if (index < collection.Count)
                {
                    current = collection.items[index];
                    index++;
                    return true;
                }
                else
                {
                    index = 0;
                    current = default(T);
                    return false;
                }
            }

            public void Reset()
            {
                index = 0;
                current = default(T);
            }

            object IEnumerator.Current => current;

            public T Current => current;
        }

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>
        /// A string that represents the current object.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override string ToString() => "Count : " + Count;

    }


}
