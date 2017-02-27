//
//  NObservableCollection.cs
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
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;

#endregion

namespace Netfox.Repository
{
    /// <summary>
    ///     An observable collection is implementation of a push-based enumeration of documents.
    /// </summary>
    /// <typeparam name="TDocument">The type of documents provided by the collection.</typeparam>
    public class NObservableCollection<TDocument> : IObservable<TDocument> where TDocument : class, new()
    {
        // TODO: Find concurrent list class that implements ICollection<T>.
        private readonly List<IObserver<TDocument>> _observers = new List<IObserver<TDocument>>();

        /// <summary>
        ///     Lock object used for control the access within this class.
        /// </summary>
        private readonly object _theLock = new object();

        private bool _running;

        /// <summary>
        ///     Creates a new observable collection.
        /// </summary>
        /// <param name="context">The repository context to which this collection is associated.</param>
        /// <param name="mongoCollection">Underlying mongo collection.</param>
        /// <param name="collectionName">The collection name.</param>
        internal NObservableCollection(NRepositoryContext context, IMongoCollection<TDocument> mongoCollection,
            string collectionName)
        {
            Context = context;
            Name = collectionName;
            MongoCollection = mongoCollection;
        }

        /// <summary>
        ///     Gets true if the current collection performs a find query that supplies data for subscribed observers.
        /// </summary>
        public bool IsBusy => !_running;

        /// <summary>
        ///     Gets the source mongo collection that is used to obtaining documents.
        /// </summary>
        public IMongoCollection<TDocument> MongoCollection { get; set; }

        /// <summary>
        ///     Gets the name of the collection.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        ///     Gets the context object that owns this observable collection.
        /// </summary>
        public NRepositoryContext Context { get; set; }

        /// <summary>
        ///     Subscribes provided observer.
        /// </summary>
        /// <param name="observer">The observer that is subscribing to this observable collection.</param>
        /// <returns>Disposable object used to unsubscribe the observer.</returns>
        public IDisposable Subscribe(IObserver<TDocument> observer)
        {
            if (!_observers.Contains(observer))
                _observers.Add(observer);
            return new Unsubscriber(_observers, observer);
        }

        /// <summary>
        ///     Executes Find on the source collection to retrieve documents that satisfy the filter through IObservable interface
        ///     to all registered subscribers.
        /// </summary>
        /// <param name="filter">The expression tree representing the filter.</param>
        /// <param name="cancellationToken">The cancellation token object.</param>
        public async Task FindAsync(Expression<Func<TDocument, bool>> filter,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            lock (_theLock)
            {
                if (_running)
                    throw new InvalidOperationException(
                        $"Cannot run {nameof(FindAsync)}. Current collection is executing the other query.");
                _running = true;
            }

            try
            {
                using (var cursor = await MongoCollection.Find(filter).ToCursorAsync(cancellationToken))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    while (await cursor.MoveNextAsync(cancellationToken))
                    {
                        foreach (var doc in cursor.Current)
                        {
                            ExecOnNext(doc);
                            cancellationToken.ThrowIfCancellationRequested();
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                ExecOnCompleted();
                _running = false;
            }
        }

        private void ExecOnCompleted()
        {
            foreach (var observer in _observers.ToArray())
                observer?.OnCompleted();
        }

        private void ExecOnNext(TDocument doc)
        {
            foreach (var observer in _observers.ToArray())
                observer?.OnNext(doc);
        }

        /// <summary>
        ///     Executes Find on observable collection using the given filter. This method can only be used if the underlying
        ///     collection is capped.
        /// </summary>
        /// <param name="filter">The filter used to create an observale collection.</param>
        /// <param name="cancellationToken">The cancellation token object.</param>
        public async Task TailCollectionFindAsync(Expression<Func<TDocument, bool>> filter,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            // TODO:test if the collection is capped collection, this can only be run on capped conllections!            
            lock (_theLock)
            {
                if (_running)
                    throw new InvalidOperationException(
                        $"Cannot run {nameof(TailCollectionFindAsync)}. Current collection is executing the other query.");
                _running = true;
            }
            // Set lastValue to the smallest value possible
            BsonValue lastId = BsonMinKey.Value;

            var options = new FindOptions<TDocument>
            {
                CursorType = CursorType.TailableAwait
            };
            var fb = new FilterDefinitionBuilder<TDocument>();
            var appliedFilter = fb.And(filter);
            try
            {
                while (true)
                {
                    // Start the cursor and wait for the initial response
                    using (var cursor = await MongoCollection.FindAsync(appliedFilter, options, cancellationToken))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        while (await cursor.MoveNextAsync(cancellationToken))
                        {
                            foreach (var doc in cursor.Current)
                            {
                                ExecOnNext(doc);
                                lastId = (doc as IDocument)?.Id;
                                cancellationToken.ThrowIfCancellationRequested();
                            }
                        }
                    }
                    // The tailable cursor died so loop through and restart it
                    // Now, we want documents that are strictly greater than the last value we saw...using ObjectId is 
                    // not a bad idea as it has tim einformation built in it...
                    appliedFilter = fb.And(filter, new BsonDocument("$gt", new BsonDocument("_id", lastId)));
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                ExecOnCompleted();
                _running = false;
            }
        }

        private class Unsubscriber : IDisposable
        {
            private readonly IObserver<TDocument> _observer;
            private readonly ICollection<IObserver<TDocument>> _observers;

            public Unsubscriber(ICollection<IObserver<TDocument>> observers, IObserver<TDocument> observer)
            {
                _observers = observers;
                _observer = observer;
            }

            public void Dispose()
            {
                if (_observer != null && _observers.Contains(_observer))
                    _observers.Remove(_observer);
            }
        }
    }
}