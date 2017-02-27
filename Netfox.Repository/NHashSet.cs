//
//  NHashSet.cs
//
//  Author:
//       Ondrej Rysavy <rysavy@fit.vutbr.cz>
//
//  Copyright (c) 2015 (c) Brno University of Technology
//
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU Lesser General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU Lesser General Public License for more details.
//
//  You should have received a copy of the GNU Lesser General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.

#region

using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;

#endregion

namespace Netfox.Repository
{
    /// <summary>
    ///     Represents a set of documents of the specified type.
    /// </summary>
    /// <typeparam name="TDocument">The type of elements in the hash set.</typeparam>
    /// <remarks>
    ///     The current NHashSet is implemented by System.Collections.Generic.HashSet and supports INotifyCollectionChanged
    ///     events.
    /// </remarks>
    public class NHashSet<TDocument> : ISet<TDocument>, INotifyCollectionChanged where TDocument : class, new()
    {
        private readonly HashSet<TDocument> _dataSet;
        private static readonly DocumentEqualityComparer EqualityComparer = new DocumentEqualityComparer();

        /// <summary>
        ///     Creates a new instance of <see cref="NHashSet{TDocument}" />.
        /// </summary>
        public NHashSet()
        {
            _dataSet = new HashSet<TDocument>(EqualityComparer);
        }

        /// <summary>
        ///     Creates a new instance of <see cref="NHashSet{TDocument}" /> and populates it with <paramref name="docs" />
        ///     enumeration.
        /// </summary>
        /// <param name="docs">Document to be added to the <see cref="NHashSet{TDocument}" /> instance.</param>
        public NHashSet(IEnumerable<TDocument> docs)
        {
            _dataSet = new HashSet<TDocument>(docs, EqualityComparer);
        }

        /// <summary>
        ///     Fired when the current object changed.
        /// </summary>
        public event NotifyCollectionChangedEventHandler CollectionChanged;

        /// <summary>
        ///     Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>An enumerator that can be used to iterate through the collection.</returns>
        public IEnumerator<TDocument> GetEnumerator()
        {
            return _dataSet.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable) _dataSet).GetEnumerator();
        }

        /// <summary>
        ///     Modifies the current set so that it contains all elements that are present in the current set, in the specified
        ///     collection, or in both.
        /// </summary>
        /// <param name="other">The collection to compare to the current set.</param>
        public void UnionWith(IEnumerable<TDocument> other)
        {
            _dataSet.UnionWith(other);
        }

        /// <summary>
        ///     Modifies the current set so that it contains only elements that are also in a specified collection.
        /// </summary>
        /// <param name="other">The collection to compare to the current set.</param>
        public void IntersectWith(IEnumerable<TDocument> other)
        {
            _dataSet.IntersectWith(other);
        }

        /// <summary>
        ///     Removes all elements in the specified collection from the current set.
        /// </summary>
        /// <param name="other">The collection to compare to the current set.</param>
        public void ExceptWith(IEnumerable<TDocument> other)
        {
            _dataSet.ExceptWith(other);
        }

        /// <summary>
        ///     Modifies the current set so that it contains only elements that are present either in the current set or in the
        ///     specified collection, but not both.
        /// </summary>
        /// <param name="other">The collection to compare to the current set.</param>
        public void SymmetricExceptWith(IEnumerable<TDocument> other)
        {
            _dataSet.SymmetricExceptWith(other);
        }

        /// <summary>
        ///     Determines whether a set is a subset of a specified collection.
        /// </summary>
        /// <param name="other">The collection to compare to the current set.</param>
        /// <returns>true if the current set is a subset of other; otherwise, false.</returns>
        public bool IsSubsetOf(IEnumerable<TDocument> other)
        {
            return _dataSet.IsSubsetOf(other);
        }

        /// <summary>
        ///     Determines whether the current set is a superset of a specified collection.
        /// </summary>
        /// <param name="other">The collection to compare to the current set.</param>
        /// <returns>true if the current set is a superset of other; otherwise, false.</returns>
        public bool IsSupersetOf(IEnumerable<TDocument> other)
        {
            return _dataSet.IsSupersetOf(other);
        }

        /// <summary>
        ///     Determines whether the current set is a proper (strict) superset of a specified collection.
        /// </summary>
        /// <param name="other">The collection to compare to the current set.</param>
        /// <returns>true if the current set is a proper superset of other; otherwise, false.</returns>
        public bool IsProperSupersetOf(IEnumerable<TDocument> other)
        {
            return _dataSet.IsProperSupersetOf(other);
        }

        /// <summary>
        ///     Determines whether the current set is a proper (strict) subset of a specified collection.
        /// </summary>
        /// <param name="other">The collection to compare to the current set.</param>
        /// <returns>true if the current set is a proper subset of other; otherwise, false.</returns>
        public bool IsProperSubsetOf(IEnumerable<TDocument> other)
        {
            return _dataSet.IsProperSubsetOf(other);
        }

        /// <summary>
        ///     Determines whether the current set overlaps with the specified collection.
        /// </summary>
        /// <param name="other">The collection to compare to the current set.</param>
        /// <returns>true if the current set and other share at least one common element; otherwise, false.</returns>
        public bool Overlaps(IEnumerable<TDocument> other)
        {
            return _dataSet.Overlaps(other);
        }

        /// <summary>
        ///     Determines whether the current set and the specified collection contain the same elements.
        /// </summary>
        /// <param name="other">The collection to compare to the current set.</param>
        /// <returns>true if the current set is equal to other; otherwise, false.</returns>
        public bool SetEquals(IEnumerable<TDocument> other)
        {
            return _dataSet.SetEquals(other);
        }

        /// <summary>
        ///     Adds the specified element to a set.
        /// </summary>
        /// <param name="item">The element to add to the set.</param>
        /// <returns>true if the element is added to the NHashSet object; false if the element is already present.</returns>
        /// <remarks>
        ///     See documentation of HashSet.Add for more information:
        ///     https://msdn.microsoft.com/en-us/library/bb353005(v=vs.110).aspx
        /// </remarks>
        public bool Add(TDocument item)
        {
            var result = _dataSet.Add(item);
            if (result)
                CollectionChanged?.Invoke(this,
                    new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, new[] {item}));
            return result;
        }

        /// <summary>
        ///     Adds an element to the current set and returns a value to indicate if the element was successfully added.
        /// </summary>
        /// <param name="item">The element to add to the set.</param>
        void ICollection<TDocument>.Add(TDocument item)
        {
            Add(item);
        }

        /// <summary>
        ///     Removes all items from the set.
        /// </summary>
        public void Clear()
        {
            _dataSet.Clear();
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        /// <summary>
        ///     Determines whether the has set contains a specific value.
        /// </summary>
        /// <param name="item">The object to locate in the set.</param>
        /// <returns>true if item is found in theset; otherwise, false.</returns>
        public bool Contains(TDocument item)
        {
            return _dataSet.Contains(item);
        }

        /// <summary>
        ///     Copies the elements of thehas set to an Array, starting at a particular Array index.
        /// </summary>
        /// <param name="array">
        ///     The one-dimensional Array that is the destination of the elements copied from the hash set. The
        ///     Array must have zero-based indexing.
        /// </param>
        /// <param name="arrayIndex">The zero-based index in array at which copying begins.</param>
        public void CopyTo(TDocument[] array, int arrayIndex)
        {
            _dataSet.CopyTo(array, arrayIndex);
        }

        /// <summary>
        ///     Removes the first occurrence of a specific object from the hash set.
        /// </summary>
        /// <param name="item">The object to remove from the hash set.</param>
        /// <returns>
        ///     true if item was successfully removed from the hash set; otherwise, false. This method also returns false if item
        ///     is not found in the original hash set.
        /// </returns>
        public bool Remove(TDocument item)
        {
            var result = _dataSet.Remove(item);
            CollectionChanged?.Invoke(this,
                new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, new[] {item}));
            return result;
        }

        /// <summary>
        ///     Gets the number of elements in the hash set.
        /// </summary>
        public int Count => _dataSet.Count;

        /// <summary>
        ///     Always <c>false</c> as the hash set ir read/write collection.
        /// </summary>
        public bool IsReadOnly => false;

        private class DocumentEqualityComparer : IEqualityComparer<TDocument>
        {
            public bool Equals(TDocument x, TDocument y)
            {
                var xId = (x as IDocument)?.Id;
                var yId = (y as IDocument)?.Id;
                return Equals(xId, yId);
            }

            public int GetHashCode(TDocument obj)
            {
                return (obj as IDocument)?.Id.GetHashCode() ?? 0;
            }
        }
    }
}