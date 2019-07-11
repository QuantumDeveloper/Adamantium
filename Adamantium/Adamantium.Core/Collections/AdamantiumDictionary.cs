using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Adamantium.Core.Collections
{
   /// <summary>
   /// Observable dictionary with binding support
   /// </summary>
   /// <typeparam name="TKey"></typeparam>
   /// <typeparam name="TValue"></typeparam>
   /// <remarks>Notofocations for binding could be switched on or off using EnableNotifications property</remarks>
   public class AdamantiumDictionary<TKey, TValue>: IDictionary<TKey, TValue>, IDictionary, INotifyPropertyChanged, INotifyCollectionChanged
   {
      private readonly Dictionary<TKey, TValue> innerDictionary;
      private readonly object syncObject = new object();

      /// <summary>
      /// Enbales/disables sending notifications for binding
      /// </summary>
      public Boolean EnableNotifications { get; set; }

      /// <summary>
      /// Constructs <see cref="AdamantiumDictionary{TKey,TValue}"/>
      /// </summary>
      /// <param name="enableNotifications">Defines whether binding support will be enabled or disabled. Default value - true.</param>
      public AdamantiumDictionary(bool enableNotifications = true)
      {
         EnableNotifications = enableNotifications;
         innerDictionary = new Dictionary<TKey, TValue>();
      }

      /// <summary>
      /// Constructs <see cref="AdamantiumDictionary{TKey,TValue}"/> with certain capacity
      /// </summary>
      /// <param name="capacity">collection capacity</param>
      /// <param name="enableNotifications">Defines whether binding support will be enabled or disabled. Default value - true.</param>
      public AdamantiumDictionary(Int32 capacity, bool enableNotifications = true)
      {
         EnableNotifications = enableNotifications;
         innerDictionary = new System.Collections.Generic.Dictionary<TKey, TValue>(capacity);
      }

      /// <summary>
      /// Constructs <see cref="AdamantiumDictionary{TKey,TValue}"/> from <see cref="IDictionary{TKey, TValue}"/>
      /// </summary>
      /// <param name="dict"><see cref="IDictionary{TKey, TValue}"/> to copy</param>
      /// <param name="enableNotifications">Defines whether binding support will be enabled or disabled. Default value - true.</param>
      public AdamantiumDictionary(IDictionary<TKey, TValue> dict, bool enableNotifications = true)
      {
         EnableNotifications = enableNotifications;
         innerDictionary = new System.Collections.Generic.Dictionary<TKey, TValue>();
         if (dict != null)
         {
            foreach (var pair in dict)
            {
               Add(pair);
            }
         }
      }

      /// <summary>
      /// Constructs <see cref="AdamantiumDictionary{TKey,TValue}"/> with certain <see cref="IEqualityComparer"/>
      /// </summary>
      /// <param name="comparer"><see cref="IEqualityComparer"/> to apply</param>
      /// <param name="enableNotifications">Defines whether binding support will be enabled or disabled. Default value - true.</param>
      public AdamantiumDictionary(IEqualityComparer<TKey> comparer, bool enableNotifications = true)
      {
         EnableNotifications = enableNotifications;
         innerDictionary = new System.Collections.Generic.Dictionary<TKey, TValue>(comparer);
      }

      /// <summary>
      /// Constructs <see cref="AdamantiumDictionary{TKey,TValue}"/> from <see cref="IDictionary{TKey, TValue}"/> with certain <see cref="IEqualityComparer"/>
      /// </summary>
      /// <param name="dict"><see cref="IDictionary{TKey, TValue}"/> to copy</param>
      /// <param name="comparer"><see cref="IEqualityComparer"/> to apply</param>
      /// <param name="enableNotifications">Defines whether binding support will be enabled or disabled. Default value - true.</param>
      public AdamantiumDictionary(IDictionary<TKey, TValue> dict, IEqualityComparer<TKey> comparer, bool enableNotifications = true)
      {
         EnableNotifications = enableNotifications;
         innerDictionary = new System.Collections.Generic.Dictionary<TKey, TValue>(comparer);
         foreach (var pair in dict)
         {
            Add(pair);
         }
      }

      /// <summary>
      /// Gets the <see cref="IEqualityComparer{TKey}"/> that is used to determine equility of keys for the dictiomary
      /// </summary>
      public IEqualityComparer<TKey> Comparer => innerDictionary.Comparer;

      /// <summary>
      /// Removes all elements from the <see cref="AdamantiumDictionary{TKey,TValue}"/> object.
      /// </summary>
      public void Clear()
      {
         lock (SyncRoot)
         {
            innerDictionary.Clear();
            NotifyReset();
         }
      }

      IDictionaryEnumerator IDictionary.GetEnumerator()
      {
         lock (SyncRoot)
         {
            return ((IDictionary) innerDictionary).GetEnumerator();
         }
      }

      void IDictionary.Remove(object key)
      {
         Remove((TKey) key);
      }

      object IDictionary.this[object key]
      {
         get
         {
            lock (SyncRoot)
            {
               return innerDictionary[(TKey)key];
            }
         }
         set
         {
            SetItem((TKey)key, (TValue)value);
         }
      }

      bool IDictionary.Contains(object key)
      {
         return ContainsKey((TKey)key);
      }

      void IDictionary.Add(object key, object value)
      {
         Add((TKey)key, (TValue)value);
      }

      void ICollection<KeyValuePair<TKey, TValue>>.Clear()
      {
         Clear();
      }

      IEnumerator IEnumerable.GetEnumerator()
      {
         return GetEnumerator();
      }

      /// <summary>
      /// Returns an enumerator that iterates through the collection.
      /// </summary>
      /// <returns>
      /// An enumerator that can be used to iterate through the collection.
      /// </returns>
      /// <filterpriority>1</filterpriority>
      public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
      {
         lock (SyncRoot)
         {
            return innerDictionary.GetEnumerator();
         }
      }

      /// <summary>
      /// Adds an item to the <see cref="AdamantiumDictionary{TKey,TValue}"/>.
      /// </summary>
      /// <param name="item">The object to add to the <see cref="AdamantiumDictionary{TKey,TValue}"/>. </param>
      public void Add(KeyValuePair<TKey, TValue> item)
      {
         Add(item.Key, item.Value);
      }


      /// <summary>
      /// Determines whether the <see cref="AdamantiumDictionary{TKey,TValue}"/> contains a specific key.
      /// </summary>
      /// <returns>
      /// true if <paramref name="item"/> is found in the <see cref="AdamantiumDictionary{TKey,TValue}"/>; otherwise, false.
      /// </returns>
      /// <param name="item"><see cref="KeyValuePair{TKey, TValue}"/> to locate in the <see cref="AdamantiumDictionary{TKey,TValue}"/>.</param>
      public bool Contains(KeyValuePair<TKey, TValue> item)
      {
         return ContainsKey(item.Key);
      }

      /// <summary>
      /// Determines whether the <see cref="AdamantiumDictionary{TKey,TValue}"/> contains a specific key.
      /// </summary>
      /// <returns>
      /// true if <paramref name="item"/> is found in the <see cref="AdamantiumDictionary{TKey,TValue}"/>; otherwise, false.
      /// </returns>
      /// <param name="item">Object> to locate in the <see cref="AdamantiumDictionary{TKey,TValue}"/>.</param>
      public bool Contains(TKey item)
      {
         return ContainsKey(item);
      }

      /// <summary>
      /// Copies the elements of the <see cref="AdamantiumDictionary{TKey,TValue}"/> to an <see cref="T:System.Array"/>, starting at a particular <see cref="T:System.Array"/> index.
      /// </summary>
      /// <param name="array">The one-dimensional <see cref="T:System.Array"/> that is the destination of the elements copied from <see cref="T:System.Collections.Generic.ICollection`1"/>. The <see cref="T:System.Array"/> must have zero-based indexing.</param><param name="arrayIndex">The zero-based index in <paramref name="array"/> at which copying begins.</param><exception cref="T:System.ArgumentNullException"><paramref name="array"/> is null.</exception><exception cref="T:System.ArgumentOutOfRangeException"><paramref name="arrayIndex"/> is less than 0.</exception><exception cref="T:System.ArgumentException">The number of elements in the source <see cref="T:System.Collections.Generic.ICollection`1"/> is greater than the available space from <paramref name="arrayIndex"/> to the end of the destination <paramref name="array"/>.</exception>
      public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
      {
         if (array == null)
         {
            throw new ArgumentNullException(nameof(array));
         }
         if ((arrayIndex < 0) || (arrayIndex > array.Length))
         {
            throw new ArgumentOutOfRangeException(
               nameof(arrayIndex));
         }
         if ((array.Length - arrayIndex) < innerDictionary.Count)
         {
            throw new ArgumentException("CopyTo() failed:  supplied array was too small");
         }

         lock (SyncRoot)
         {
            foreach (var entry in innerDictionary)
               array[arrayIndex++] = new KeyValuePair<TKey, TValue>(entry.Key, entry.Value);
         }
      }

      /// <summary>
      /// Removes the first occurrence of a specific object from the <see cref="T:System.Collections.Generic.ICollection`1"/>.
      /// </summary>
      /// <returns>
      /// true if <paramref name="item"/> was successfully removed from the <see cref="T:System.Collections.Generic.ICollection`1"/>; otherwise, false. This method also returns false if <paramref name="item"/> is not found in the original <see cref="T:System.Collections.Generic.ICollection`1"/>.
      /// </returns>
      /// <param name="item">The object to remove from the <see cref="T:System.Collections.Generic.ICollection`1"/>.</param><exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.ICollection`1"/> is read-only.</exception>
      public bool Remove(KeyValuePair<TKey, TValue> item)
      {
         return Remove(item.Key);
      }

      /// <summary>
      /// Copies the elements of the <see cref="AdamantiumDictionary{TKey,TValue}"/> to an <see cref="T:System.Array"/>, starting at a particular <see cref="T:System.Array"/> index.
      /// </summary>
      /// <param name="array">The one-dimensional <see cref="T:System.Array"/> that is the destination of the elements copied from <see cref="AdamantiumDictionary{TKey,TValue}"/>. The <see cref="T:System.Array"/> must have zero-based indexing. </param>
      /// <param name="index">The zero-based index in <paramref name="array"/> at which copying begins. </param>
      /// <exception cref="T:System.ArgumentNullException"><paramref name="array"/> is null. </exception>
      /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index"/> is less than zero. </exception>
      /// <exception cref="T:System.ArgumentException"><paramref name="array"/> is multidimensional.-or- The number of elements in the source <see cref="AdamantiumDictionary{TKey,TValue}"/> is greater than the available space from <paramref name="index"/> to the end of the destination <paramref name="array"/>.-or-The type of the source <see cref="AdamantiumDictionary{TKey,TValue}"/> cannot be cast automatically to the type of the destination <paramref name="array"/>.</exception><filterpriority>2</filterpriority>
      public void CopyTo(Array array, int index)
      {
         lock (SyncRoot)
         {
            ((ICollection)innerDictionary).CopyTo(array, index);
         }
      }

      /// <summary>
      /// Gets the number of elements contained in the <see cref="AdamantiumDictionary{TKey,TValue}"/>.
      /// </summary>
      /// <returns>
      /// The number of elements contained in the <see cref="AdamantiumDictionary{TKey,TValue}"/>.
      /// </returns>
      /// <filterpriority>2</filterpriority>
      public int Count
      {
         get
         {
            lock (SyncRoot)
            {
               return innerDictionary.Count;
            }
         }
      }

      /// <summary>
      /// Gets an object that can be used to synchronize access to the <see cref="T:System.Collections.ICollection"/>.
      /// </summary>
      /// <returns>
      /// An object that can be used to synchronize access to the <see cref="T:System.Collections.ICollection"/>.
      /// </returns>
      /// <filterpriority>2</filterpriority>
      public object SyncRoot => syncObject;

      /// <summary>
      /// Gets a value indicating whether access to the <see cref="T:System.Collections.ICollection"/> is synchronized (thread safe).
      /// </summary>
      /// <returns>
      /// true if access to the <see cref="T:System.Collections.ICollection"/> is synchronized (thread safe); otherwise, false.
      /// </returns>
      /// <filterpriority>2</filterpriority>
      public bool IsSynchronized => true;

      int ICollection<KeyValuePair<TKey, TValue>>.Count
      {
         get
         {
            lock (SyncRoot)
            {
               return innerDictionary.Count;
            }
         }
      }

      ICollection IDictionary.Values => Values;

      bool IDictionary.IsReadOnly => false;

      /// <summary>
      /// Gets a value indicating whether the <see cref="T:System.Collections.IDictionary"/> object has a fixed size.
      /// </summary>
      /// <returns>
      /// true if the <see cref="T:System.Collections.IDictionary"/> object has a fixed size; otherwise, false.
      /// </returns>
      /// <filterpriority>2</filterpriority>
      public bool IsFixedSize => false;

      bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => false;

      /// <summary>
      /// Determines whether the <see cref="AdamantiumDictionary{TKey,TValue}"/> contains an element with the specified key.
      /// </summary>
      /// <param name="key">The key to locate in the <see cref="AdamantiumDictionary{TKey,TValue}"/>.
      /// </param><exception cref="T:System.ArgumentNullException"><paramref name="key"/> is null.</exception>
      /// /// <returns>
      /// true if the <see cref="AdamantiumDictionary{TKey,TValue}"/> contains an element with the key, otherwise - false.
      /// </returns>
      public bool ContainsKey(TKey key)
      {
         lock (SyncRoot)
         {
            return innerDictionary.ContainsKey(key);
         }
      }

      /// <summary>
      /// Determines whether the <see cref="AdamantiumDictionary{TKey,TValue}"/> contains an element with the specified value.
      /// </summary>
      /// <param name="value">The value to locate in the <see cref="AdamantiumDictionary{TKey,TValue}"/>.</param>
      /// <returns>
      /// true if the <see cref="AdamantiumDictionary{TKey,TValue}"/> contains an element with the value, otherwise - false.
      /// </returns>
      public bool ContainsValue(TValue value)
      {
         lock (SyncRoot)
         {
            return innerDictionary.ContainsValue(value);
         }
      }

      /// <summary>
      /// Adds an element with the provided key and value to the <see cref="AdamantiumDictionary{TKey,TValue}"/>.
      /// </summary>
      /// <param name="key">The object to use as the key of the element to add.</param><param name="value">The object to use as the value of the element to add.</param><exception cref="T:System.ArgumentNullException"><paramref name="key"/> is null.</exception><exception cref="T:System.ArgumentException">An element with the same key already exists in the <see cref="T:System.Collections.Generic.IDictionary`2"/>.</exception><exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.IDictionary`2"/> is read-only.</exception>
      public void Add(TKey key, TValue value)
      {
         lock (SyncRoot)
         {
            Validate?.Invoke(value);
            innerDictionary.Add(key, value);
            if (EnableNotifications)
            {
               var index = GetIndexForKey(key);
               NotifyAdd(new KeyValuePair<TKey, TValue>(key, value), index);
            }
         }
      }

      /// <summary>
      /// Removes the element with the specified key from the <see cref="AdamantiumDictionary{TKey,TValue}"/>.
      /// </summary>
      /// <returns>
      /// true if the element is successfully removed; otherwise, false.  This method also returns false if <paramref name="key"/> was not found in the original <see cref="AdamantiumDictionary{TKey,TValue}"/>.
      /// </returns>
      /// <param name="key">The key of the element to remove.</param><exception cref="T:System.ArgumentNullException"><paramref name="key"/> is null.</exception>
      public bool Remove(TKey key)
      {
         lock (SyncRoot)
         {
            bool result;
            try
            {
               if (EnableNotifications)
               {
                  var value = innerDictionary[key];
                  var index = GetIndexForKey(key);
                  NotifyRemove(new KeyValuePair<TKey, TValue>(key, value), index);
               }
               result = innerDictionary.Remove(key);
            }
            finally
            {
               innerDictionary.Remove(key);
            }
            return result;
         }
      }

      /// <summary>
      /// Gets the value associated with the specified key.
      /// </summary>
      /// <returns>
      /// true if the object that implements <see cref="AdamantiumDictionary{TKey,TValue}"/> contains an element with the specified key; otherwise, false.
      /// </returns>
      /// <param name="key">The key whose value to get.</param><param name="value">When this method returns, the value associated with the specified key, if the key is found; otherwise, the default value for the type of the <paramref name="value"/> parameter. This parameter is passed uninitialized.</param><exception cref="T:System.ArgumentNullException"><paramref name="key"/> is null.</exception>
      public bool TryGetValue(TKey key, out TValue value)
      {
         lock (SyncRoot)
         {
            return innerDictionary.TryGetValue(key, out value);
         }
      }

      /// <summary>
      /// Gets or sets the element with the specified key.
      /// </summary>
      /// <returns>
      /// The element with the specified key.
      /// </returns>
      /// <param name="key">The key of the element to get or set.</param><exception cref="T:System.ArgumentNullException"><paramref name="key"/> is null.</exception><exception cref="T:System.Collections.Generic.KeyNotFoundException">The property is retrieved and <paramref name="key"/> is not found.</exception><exception cref="T:System.NotSupportedException">The property is set and the <see cref="AdamantiumDictionary{TKey,TValue}"/> is read-only.</exception>
      public TValue this[TKey key]
      {
         get
         {
            lock (SyncRoot)
            {
               return innerDictionary[key];
            }
         }
         set
         {
            SetItem(key, value);
         }
      }

      private void SetItem(TKey key, TValue value)
      {
         lock (SyncRoot)
         {
            Validate?.Invoke(value);
            var val = innerDictionary[key];
            innerDictionary[key] = value;
            if (EnableNotifications)
            {
               int index = GetIndexForKey(key);
               NotifyReplace(new KeyValuePair<TKey, TValue>(key, val), new KeyValuePair<TKey, TValue>(key, value), index);
            }
         }
      }

      /// <summary>
      /// Keys collection
      /// </summary>
      public System.Collections.Generic.Dictionary<TKey, TValue>.KeyCollection Keys
      {
         get
         {
            lock (SyncRoot)
            {
               return innerDictionary.Keys;
            }
         }
      }

      /// <summary>
      /// Values collection
      /// </summary>
      public System.Collections.Generic.Dictionary<TKey, TValue>.ValueCollection Values
      {
         get
         {
            lock (SyncRoot)
            {
               return innerDictionary.Values;
            }
         }
      }

      ICollection<TKey> IDictionary<TKey, TValue>.Keys => Keys;

      ICollection IDictionary.Keys => Keys;

      ICollection<TValue> IDictionary<TKey, TValue>.Values => Values;

      /// <summary>
      /// Fires when Keys, Values, Item[] or Count properties were changed
      /// </summary>
      public event PropertyChangedEventHandler PropertyChanged;

      /// <summary>
      /// Fires when items added, removed or replaced in collectionn
      /// </summary>
      public event NotifyCollectionChangedEventHandler CollectionChanged;

      /// <summary>
      /// Gets or sets a validation routine that can be used to validate items before they are
      /// added.
      /// </summary>
      public Action<TValue> Validate { get; set; }

      /// <summary>
      /// Raises the <see cref="CollectionChanged"/> event with an add action.
      /// </summary>
      /// <param name="pair">KeyValuePair that was added.</param>
      /// <param name="index">The starting index.</param>
      private void NotifyAdd(KeyValuePair<TKey, TValue> pair, int index)
      {
         if (CollectionChanged != null)
         {
            var e = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, pair, index);
            CollectionChanged(this, e);
         }

         NotifyChanged();
      }

      /// <summary>
      /// Raises the <see cref="PropertyChanged"/> event when the <see cref="Count"/> property
      /// changes.
      /// </summary>
      private void NotifyChanged()
      {
         PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Count"));
         PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
         PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Keys"));
         PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Values"));
      }

      /// <summary>
      /// Raises the <see cref="CollectionChanged"/> event with a remove action.
      /// </summary>
      /// <param name="pair">The items that were removed.</param>
      /// <param name="index">The starting index.</param>
      private void NotifyRemove(KeyValuePair<TKey, TValue> pair, int index)
      {
         if (CollectionChanged != null)
         {
            var e = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, pair, index);
            CollectionChanged(this, e);
         }

         NotifyChanged();
      }

      /// <summary>
      /// Raises the <see cref="CollectionChanged"/> event with a remove action.
      /// </summary>
      /// <param name="oldPair">The item that was removed.</param>
      /// <param name="newPair">The item that was added.</param>
      /// <param name="index">The starting index.</param>
      private void NotifyReplace(KeyValuePair<TKey, TValue> oldPair, KeyValuePair<TKey, TValue> newPair, int index)
      {
         if (CollectionChanged != null)
         {
            var e = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, newPair, oldPair, index);
            CollectionChanged(this, e);
         }

         NotifyChanged();
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

         NotifyChanged();
      }

      /// <summary>
      /// Returns a string that represents the current object.
      /// </summary>
      /// <returns>
      /// A string that represents the current object.
      /// </returns>
      /// <filterpriority>2</filterpriority>
      public override string ToString() => "Count : " + Count;

      private int GetIndexForKey(TKey key)
      {
         int index = -1;
         foreach (var kvp in innerDictionary)
         {
            index++;
            if (Equals(kvp.Key, key))
            {
               return index;
            }
         }
         return index;
      }
   }
}
