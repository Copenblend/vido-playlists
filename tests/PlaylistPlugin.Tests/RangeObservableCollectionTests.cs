using System.Collections.Specialized;
using PlaylistPlugin.Models;
using Xunit;

namespace PlaylistPlugin.Tests;

public class RangeObservableCollectionTests
{
    [Fact]
    public void AddRange_AddsItemsAndRaisesSingleReset()
    {
        var collection = new RangeObservableCollection<int>();
        var eventCount = 0;
        var lastAction = NotifyCollectionChangedAction.Add;

        collection.CollectionChanged += (_, e) =>
        {
            eventCount++;
            lastAction = e.Action;
        };

        collection.AddRange([1, 2, 3]);

        Assert.Equal(3, collection.Count);
        Assert.Equal(1, eventCount);
        Assert.Equal(NotifyCollectionChangedAction.Reset, lastAction);
    }

    [Fact]
    public void AddRange_WithEmptyInput_DoesNotRaiseEvents()
    {
        var collection = new RangeObservableCollection<int>();
        var eventCount = 0;

        collection.CollectionChanged += (_, _) => eventCount++;

        collection.AddRange([]);

        Assert.Empty(collection);
        Assert.Equal(0, eventCount);
    }

    [Fact]
    public void RemoveRange_RemovesMatchesAndRaisesSingleReset()
    {
        var collection = new RangeObservableCollection<int>([1, 2, 3, 4, 5]);
        var eventCount = 0;

        collection.CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
                eventCount++;
        };

        var removed = collection.RemoveRange(i => i % 2 == 0);

        Assert.Equal(2, removed);
        Assert.Equal([1, 3, 5], collection.ToArray());
        Assert.Equal(1, eventCount);
    }

    [Fact]
    public void ReplaceAll_ReplacesContentsAndRaisesSingleReset()
    {
        var collection = new RangeObservableCollection<string>(["a", "b"]);
        var eventCount = 0;
        var lastAction = NotifyCollectionChangedAction.Add;

        collection.CollectionChanged += (_, e) =>
        {
            eventCount++;
            lastAction = e.Action;
        };

        collection.ReplaceAll(["x", "y", "z"]);

        Assert.Equal(["x", "y", "z"], collection.ToArray());
        Assert.Equal(1, eventCount);
        Assert.Equal(NotifyCollectionChangedAction.Reset, lastAction);
    }

    [Fact]
    public void AddRange_WithNull_Throws()
    {
        var collection = new RangeObservableCollection<int>();

        Assert.Throws<ArgumentNullException>(() => collection.AddRange(null!));
    }
}
