using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace PlaylistPlugin.Models;

/// <summary>
/// Represents an <see cref="ObservableCollection{T}"/> that supports efficient bulk operations.
/// </summary>
/// <typeparam name="T">The type of elements in the collection.</typeparam>
public class RangeObservableCollection<T> : ObservableCollection<T>
{
    private bool _suppressNotifications;

    /// <summary>
    /// Initializes an empty collection.
    /// </summary>
    public RangeObservableCollection()
    {
    }

    /// <summary>
    /// Initializes the collection with the specified items.
    /// </summary>
    /// <param name="items">The items to initialize the collection with.</param>
    public RangeObservableCollection(IEnumerable<T> items)
        : base(items)
    {
    }

    /// <summary>
    /// Adds multiple items and emits a single reset change notification.
    /// </summary>
    /// <param name="items">The items to add.</param>
    public void AddRange(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var bufferedItems = items as ICollection<T> ?? items.ToList();
        if (bufferedItems.Count == 0) return;

        _suppressNotifications = true;
        try
        {
            foreach (var item in bufferedItems)
            {
                Items.Add(item);
            }
        }
        finally
        {
            _suppressNotifications = false;
        }

        RaiseReset();
    }

    /// <summary>
    /// Removes all items that match a predicate and emits a single reset change notification.
    /// </summary>
    /// <param name="predicate">Predicate used to select items to remove.</param>
    /// <returns>The number of removed items.</returns>
    public int RemoveRange(Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        var toRemove = Items.Where(predicate).ToList();
        if (toRemove.Count == 0) return 0;

        _suppressNotifications = true;
        try
        {
            foreach (var item in toRemove)
            {
                Items.Remove(item);
            }
        }
        finally
        {
            _suppressNotifications = false;
        }

        RaiseReset();
        return toRemove.Count;
    }

    /// <summary>
    /// Replaces all existing items with the provided items and emits a single reset notification.
    /// </summary>
    /// <param name="items">The new collection contents.</param>
    public void ReplaceAll(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var bufferedItems = items as ICollection<T> ?? items.ToList();

        _suppressNotifications = true;
        try
        {
            Items.Clear();
            foreach (var item in bufferedItems)
            {
                Items.Add(item);
            }
        }
        finally
        {
            _suppressNotifications = false;
        }

        RaiseReset();
    }

    /// <inheritdoc />
    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (_suppressNotifications) return;
        base.OnCollectionChanged(e);
    }

    private void RaiseReset()
    {
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}