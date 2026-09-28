using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Adamantium.Core.Collections
{
    public class ReadOnlyTrackingCollection<T> : ReadOnlyCollection<T>, INotifyCollectionChanged, INotifyPropertyChanged
    {
        private TrackingCollection<T> _localCollection;

        public ReadOnlyTrackingCollection(TrackingCollection<T> collection) : base(collection)
        {
            _localCollection = collection;
            _localCollection.CollectionChanged += TrackingCollectionChanged;
            _localCollection.PropertyChanged += CollectionPropertyChanged;
        }

        private void CollectionPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(e);
        }

        private void TrackingCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            OnCollectionChanged(e);
        }

        protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            CollectionChanged?.Invoke(this, e);
        }

        protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, e);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public event NotifyCollectionChangedEventHandler CollectionChanged;
    }
}
