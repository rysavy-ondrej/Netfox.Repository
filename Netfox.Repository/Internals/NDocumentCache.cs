//
//  NDocumentCache.cs
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

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using JetBrains.Annotations;

#endregion

namespace Netfox.Repository.Internals
{
    /// <summary>
    ///     This dictionary enables to maintain objects that can be in a dead state.
    /// </summary>
    /// <typeparam name="TKey">The type of key that uniquely identifies items in the cache.</typeparam>
    /// <typeparam name="TValue">The type of value maintained in the cache.</typeparam>
    /// <remarks>
    ///     The cache maintains objects that can be either alive or dead. Method <see cref="Flush" />
    ///     can be used for removing dead objects from the cache.
    ///     When an object is added to the cache it is tested if there is a dead key. If so, the objects is updated.
    ///     All read operations retrieves only live entries from the cache.
    ///     Function to determine whether the entry is live or dead is provided when the
    ///     <see cref="NDocumentCache{TKey,TValue}" />
    ///     is created.
    /// </remarks>
    public class NDocumentCache<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
    {
        private readonly ReaderWriterLockSlim _cacheLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        private readonly ConcurrentDictionary<TKey, TValue> _dictionary;
        private readonly Func<TKey, TValue, bool> _isDeadFunc;
        private int _gcLastCollectionCount = GC.CollectionCount(0);
        private int? _lastKnownCountValue;

        /// <summary>
        ///     Creates a new instance of <see cref="NDocumentCache{TKey,TValue}" /> obejct using provided
        ///     predicate to test liveness of cache entries.
        /// </summary>
        /// <param name="isDeadFunc">A predicate that can be used for testing the liveness of cache entries. </param>
        public NDocumentCache(Func<TKey, TValue, bool> isDeadFunc)
        {
            _isDeadFunc = isDeadFunc;
            _dictionary = new ConcurrentDictionary<TKey, TValue>();
        }


        /// <summary>
        ///     Flushes all dead documents from the cache.
        /// </summary>
        /// <returns>The maximum number of entries that can be removed from the cache during the call of this method.</returns>
        /// <remarks>
        ///     Calling this method causes that other access to the <see cref="NDocumentCache{TKey,TValue}" /> object is puased
        ///     till
        ///     this method ends. Specifying <paramref name="flushSize" /> can help to improve the performance. Thus,
        ///     <see cref="Flush" /> method
        ///     can be called repeatedly to remove smaller chunks of dead items rather than to perform cache compacting as a one
        ///     big
        ///     task.
        /// </remarks>
        public int Flush(int flushSize = int.MaxValue)
        {
            _cacheLock.EnterWriteLock();
            try
            {
                var removedItems = 0;
                var deadObjects = _dictionary.Where(x => _isDeadFunc(x.Key, x.Value)).Take(flushSize);
                // have dead objects, but we need to safely remove it: 
                foreach (var deadItem in deadObjects)
                {
                    TValue value;
                    var result = _dictionary.TryRemove(deadItem.Key, out value);
                    if (result) removedItems++;
                }
                _lastKnownCountValue = null;
                return removedItems;
            }
            finally
            {
                _cacheLock.ExitWriteLock();
            }
        }

        /// <summary>
        ///     Adds a cache entry into the cache using the specified <typeparamref name="TKey"/> value.
        /// </summary>
        /// <param name="key">Key value.</param>
        /// <param name="addValueFactory">The function used to generate a value for an absent key.</param>
        /// <param name="updateValueFactory">The function used to regenerate a dead entry.</param>
        /// <returns>If a cache entry with the same key exists, the existing cache entry; otherwise, null.</returns>
        /// <remarks>
        ///     This is a complex methods. If the cache has a cache entry
        ///     with the same key as the specified key and this entry is not dead, the method returns the existing entry.
        ///     If there is no existing cache entry, the method creates a new one by using the <paramref name="addValueFactory" />
        ///     method.
        ///     If there is existing cache entry but it is marked as dead, the method <paramref name="updateValueFactory" />
        ///     is used to recreate the cache entry.
        /// </remarks>
        public TValue AddOrGetExisting(TKey key, Func<TKey, TValue> addValueFactory,
            Func<TKey, TValue, TValue> updateValueFactory)
        {
            _cacheLock.EnterWriteLock();
            try
            {
                return _dictionary.AddOrUpdate(key,
                    k => // addValueFactory
                    {
                        _lastKnownCountValue++;
                        return addValueFactory(k);
                    },
                    (k, v) => // updateValueFactory
                    {
                        if (_isDeadFunc(k, v))
                        {
                            _lastKnownCountValue++;
                            return updateValueFactory(k, v);
                        }
                        return v;
                    });
            }
            finally
            {
                _cacheLock.ExitWriteLock();
            }
        }

        /// <summary>
        ///     Inserts a cache entry into the cache by using a key and a value.
        /// </summary>
        /// <param name="key">Inserts a cache entry into the cache by using a key and a value.</param>
        /// <param name="value">Inserts a cache entry into the cache by using a key and a value.</param>
        /// <remarks>
        ///     The Set method always puts a cache value in the cache, regardless whether an entry already
        ///     exists with the same key.If the specified entry does not exist, a new cache entry is inserted.
        ///     If the specified entry exists, it is replaced with the provided value.
        /// </remarks>
        public void Set(TKey key, TValue value)
        {
            _cacheLock.EnterWriteLock();
            try
            {
                _dictionary.AddOrUpdate(key,
                    k => // addValueFactory
                    {
                        _lastKnownCountValue++;
                        return value;
                    },
                    (k, v) => // updateValueFactory
                    {
                        if (_isDeadFunc(k, v))
                            _lastKnownCountValue++;
                        return value;
                    });
            }
            finally
            {
                _cacheLock.ExitWriteLock();
            }
        }

        /// <summary>
        ///     Removes all entries from the cache.
        /// </summary>
        public void Clear()
        {
            _cacheLock.EnterWriteLock();
            try
            {
                _dictionary.Clear();
                _lastKnownCountValue = null;
            }
            finally
            {
                _cacheLock.ExitWriteLock();
            }
        }

        /// <summary>
        ///     Gets the toal entries allocated in the cache including dead entries.
        /// </summary>
        public int Capacity => _dictionary.Count;

        /// <summary>
        ///     Returns the approximate number of live cache entries in the cache. Note that this method may be expensive.
        /// </summary>
        /// <returns>The number of live entries in the cache.</returns>
        public int GetApproximateCount()
        {
            var currentGcCount = GC.CollectionCount(0);
            if (_gcLastCollectionCount + 10 < currentGcCount)
            {
                _lastKnownCountValue = null;
                _gcLastCollectionCount = currentGcCount;
            }

            if (_lastKnownCountValue == null)
            {
                _lastKnownCountValue = _dictionary.Count(x => !_isDeadFunc(x.Key, x.Value));
            }
            return _lastKnownCountValue.Value;
        }

        /// <summary>
        ///     Gets the exact number of live items in the cache. Note that this operation can be expensive for
        ///     cache objects maintaining many objects.
        /// </summary>
        /// <returns></returns>
        public int GetCount()
        {
            _gcLastCollectionCount = GC.CollectionCount(0);
            _lastKnownCountValue = _dictionary.Count(x => !_isDeadFunc(x.Key, x.Value));
            return _lastKnownCountValue.Value;
        }

        /// <summary>
        ///     Determines whether a cache entry exists in the cache.
        /// </summary>
        /// <param name="key">A unique identifier for the cache entry to search for.</param>
        /// <returns>true if the cache contains a cache entry whose key matches key; otherwise, false.</returns>
        public bool Contains(TKey key)
        {
            _cacheLock.EnterReadLock();
            try
            {
                TValue value;
                return _dictionary.TryGetValue(key, out value) && !_isDeadFunc(key, value);
            }
            finally
            {
                _cacheLock.ExitReadLock();
            }
        }

        /// <summary>
        ///     Removes a cache entry from the cache.
        /// </summary>
        /// <param name="key">A unique identifier for the cache entry to remove. </param>
        /// <returns>If the entry is found in the cache, the removed cache entry; otherwise, null.</returns>
        /// <exception cref="ArgumentNullException">key is null.</exception>
        public TValue Remove([NotNull] TKey key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            _cacheLock.EnterWriteLock();
            try
            {
                TValue value;
                if (_dictionary.TryRemove(key, out value))
                {
                    _lastKnownCountValue--;
                    return value;
                }
                return default(TValue);
            }
            finally
            {
                _cacheLock.ExitWriteLock();
            }
        }

        /// <summary>
        ///     Inserts a cache entry into the cache by using a key and a value
        /// </summary>
        /// <param name="key">A unique identifier for the cache entry to get. </param>
        /// <param name="value">A reference to the cache entry that is identified by key, if the entry exists; otherwise, null.</param>
        /// <returns><c>true</c> if a cache entry exists; otherwise <c>false</c>.</returns>
        public bool TryGetValue(TKey key, out TValue value)
        {
            _cacheLock.EnterReadLock();
            try
            {
                TValue outValue;
                if (_dictionary.TryGetValue(key, out outValue) && !_isDeadFunc(key, outValue))
                {
                    value = outValue;
                    return true;
                }
                value = default(TValue);
                return false;
            }
            finally
            {
                _cacheLock.ExitReadLock();
            }
        }

        /// <summary>
        ///     Gets an enumerator for the current cache. Please, check semantics of this operation.
        /// </summary>
        /// <returns>An enumerator for the current cache.</returns>
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return _dictionary.Where(x => !_isDeadFunc(x.Key, x.Value)).GetEnumerator();
        }

        /// <summary>
        ///     Gets an enumerator for the current cache.
        /// </summary>
        /// <returns>An enumerator for the current cache.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable) _dictionary).GetEnumerator();
        }

        /// <summary>
        ///     Gets the collection that contains all cache entries.
        /// </summary>
        public IEnumerable<TValue> Values => _dictionary.Values;
    }
}
