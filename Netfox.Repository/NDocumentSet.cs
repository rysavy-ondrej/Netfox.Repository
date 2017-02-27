//
//  NDocumentSet.cs
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
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using MongoDB.Bson;
using MongoDB.Driver;
using Netfox.Repository.Internals;

#endregion

namespace Netfox.Repository
{

    /// <summary>
    /// An NDocumentSet represents the collection of all entities in the context, or that can be queried from the database,
    /// of a given type. This is base class that provides access to <see cref="NDocumentSet{TDocument}"/> when type annotation
    /// cannot be maintained.
    /// </summary>
    public abstract class NDocumentSet : IEnumerable
    {   
        /// <summary>
        /// Gets the type of the object in this <see cref="NDocumentSet"/>.
        /// </summary>
        public abstract Type ElementType { get; }

        /// <summary>
        /// Gets the repository context to which this document set is associated.
        /// </summary>
        public NRepositoryContext Context { get; protected set; }
        /// <summary>
        /// Gets the name of the collection.
        /// </summary>
        public string Name { get; protected set; }


        /// <summary>
        ///     Adds the given document to the context underlying the set in the
        ///     <see cref="DocumentState.Added" /> state such that it will be inserted into the
        ///     database when <see cref="NRepositoryContext.SaveChangesAsync()" /> is called.
        /// </summary>
        /// <param name="document">The document to add.</param>
        /// <returns>
        ///     The <see cref="NDocumentEntry{TDocument}" /> object for the document. This object provides
        ///     access to information and actions on the tracked document.
        /// </returns>
        public abstract NDocumentEntry AddDocument([NotNull] object document);

        public abstract NDocumentEntry AttachDocument([NotNull] object document);

        public abstract NDocumentEntry RemoveDocument([NotNull] object document);

        public abstract NDocumentEntry UpdateDocument([NotNull] object document);

        public abstract Task<NDocumentEntry> FindDocumentAsync([NotNull] ObjectId key);

        /// <summary>
        /// Casts the current <see cref="NDocumentSet"/> to <see cref="NDocumentSet{TDocument}"/> can contain elements of the required type. 
        /// </summary>
        /// <typeparam name="TDocument">A type of elements.</typeparam>
        /// <returns>The typed version of <see cref="NDocumentSet{TDocument}"/> if the current <see cref="NDocumentSet"/> can contain elements of the required type.</returns>
        public abstract NDocumentSet<TDocument> Cast<TDocument>() where TDocument : class, new(); 
        /// <summary>
        ///     Counts the total entities in the repository.
        /// </summary>
        /// <returns>Count of entities in the collection.</returns>
        public abstract Task<long> CountAsync();

        /// <summary>
        ///  Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>An enumerator that can be used to iterate through the collection.</returns>
        public abstract IEnumerator GetEnumerator();
    }

    /// <summary>
    ///     An NDocumentSet represents the collection of all entities in the context, or that can be queried from the database,
    ///     of a given type.
    ///     NDocumentSet objects are created from a NRepositoryContext using the NRepositoryContext.Collection method.
    /// </summary>
    /// <typeparam name="TDocument">A type of documents in this document set.</typeparam>
    /// <remarks>
    ///     Currently NDocumentSets are IQueryables and using them results in a database query. However, they also have Add,
    ///     Remove, etc. and so act as a way to introduce documents into the unit of work, etc. But since the query is still
    ///     a database query it means that those new entities are not returned when the set is enumerated.
    /// </remarks>
    public class NDocumentSet<TDocument> : NDocumentSet, IQueryable<TDocument> where TDocument : class, new()
    {
        /// <summary>
        /// Creates a new <see cref="NDocumentSet{TDocument}"/> according to the provided arguments.
        /// </summary>
        /// <param name="context">The repository context instance.</param>
        /// <param name="mongoCollection">The underlaying mongo collection object.</param>
        /// <param name="collectionName">The name of the collection.</param>
        /// <param name="expression">The expression used to construct this document set.</param>
        internal NDocumentSet(NRepositoryContext context, IMongoCollection<TDocument> mongoCollection,
            string collectionName, Expression expression)
        {
            this.Context = context;
            this.Name = collectionName;
            this.MongoCollection = mongoCollection;
            this.Expression = expression;
            this.Provider = new NQueryProvider();
        }

        /// <summary>
        /// Gets the underlying mongo collection object.
        /// </summary>
        public IMongoCollection<TDocument> MongoCollection { get; }

        private static FilterDefinitionBuilder<TDocument> Filter => Builders<TDocument>.Filter;

        #region Implementation of IQueryable
        /// <summary>
        /// Gets the expression tree that is associated with the instance of IQueryable.
        /// </summary>
        public Expression Expression { get; }
        /// <summary>
        /// Gets the type of the element(s) that are returned when the expression tree associated with this instance of IQueryable is executed.
        /// </summary>
        public override Type ElementType => typeof (TDocument);
        /// <summary>
        /// Gets the query provider that is associated with this data source. 
        /// </summary>
        public IQueryProvider Provider { get; }
        #endregion

        /// <summary>
        ///     Adds the given document to the context underlying the set in the
        ///     <see cref="DocumentState.Added" /> state such that it will be inserted into the
        ///     database when <see cref="NRepositoryContext.SaveChangesAsync()" /> is called.
        /// </summary>
        /// <param name="document">The document to add.</param>
        /// <returns>
        ///     The <see cref="NDocumentEntry{TDocument}" /> object for the document. This object provides
        ///     access to information and actions on the tracked document.
        /// </returns>
        public virtual NDocumentEntry<TDocument> Add([NotNull] TDocument document)
        {
            return Context.TrackObject(this, document, DocumentState.Added);
        }

        /// <summary>
        ///     Begins tracking the given document in the <see cref="DocumentState.Unchanged" /> state such that no
        ///     operation will be performed when <see cref="NRepositoryContext.SaveChangesAsync()" /> is called.
        /// </summary>
        /// <param name="document"> The document to attach. </param>
        /// <returns>
        ///     The <see cref="NDocumentEntry" /> for the entity. This entry provides access to
        ///     information the context is tracking for the entity and the ability to perform
        ///     actions on the entity.
        /// </returns>
        public virtual NDocumentEntry<TDocument> Attach([NotNull] TDocument document)
        {
            return Context.TrackObject(this, document, DocumentState.Unchanged);
        }

        /// <summary>
        ///     Begins tracking the given entity in the <see cref="DocumentState.Deleted" /> state such that it will
        ///     be removed from the database when <see cref="NRepositoryContext.SaveChangesAsync()" /> is called.
        /// </summary>
        /// <remarks>
        ///     If the entity is already tracked in the <see cref="DocumentState.Added" /> state then the context will
        ///     stop tracking the entity (rather than marking it as <see cref="DocumentState.Deleted" />) since the
        ///     entity was previously added to the context and does not exist in the database.
        /// </remarks>
        /// <param name="document"> The entity to remove. </param>
        /// <returns>
        ///     The <see cref="NDocumentEntry{TDocument}" /> for the entity. This entry provides access to
        ///     information the context is tracking for the document and the ability to perform
        ///     actions on the entity.
        /// </returns>
        public virtual NDocumentEntry<TDocument> Remove([NotNull] TDocument document)
        {
            return Context.TrackObject(this, document, DocumentState.Deleted);
        }

        /// <summary>
        ///     <para>
        ///         Begins tracking the given document in the <see cref="DocumentState.Modified" /> state such that it will
        ///         be updated in the database when <see cref="NRepositoryContext.SaveChangesAsync()" /> is called.
        ///     </para>
        ///     <para>
        ///         All properties of the documents will be marked as modified. To mark only some properties as modified, use
        ///         <see cref="Attach(TDocument)" /> to begin tracking the document in the <see cref="DocumentState.Unchanged" />
        ///         state and then use the returned <see cref="NDocumentEntry" /> to mark the desired properties as modified.
        ///     </para>
        /// </summary>
        /// <param name="document"> The document to update. </param>
        /// <returns>
        ///     The <see cref="NDocumentEntry" /> for the document. This entry provides access to
        ///     information the context is tracking for the entity and the ability to perform
        ///     actions on the entity.
        /// </returns>
        public virtual NDocumentEntry<TDocument> Update([NotNull] TDocument document)
        {
            return Context.TrackObject(this, document, DocumentState.Modified);
        }

        /// <summary>
        ///     Begins tracking the given documents in the <see cref="DocumentState.Added" /> state such that they will
        ///     be inserted into the database when <see cref="NRepositoryContext.SaveChangesAsync()" /> is called.
        /// </summary>
        /// <param name="documents"> The documents to add. </param>
        public virtual void AddRange([NotNull] IEnumerable<TDocument> documents)
        {
            foreach (var doc in documents)
            {
                Add(doc);
            }
        }

        #region Methods for direct access to underlying Mongo Collection.

        /// <summary>
        ///     Finds a document with the given oid key. If a document with the given oid exists in the context,
        ///     then it is returned immediately without making a request to the store. Otherwise, a request is made to the
        ///     repository for
        ///     a document with the given oid and this document, if found, is attached to the context and returned.
        ///     If no document is found in the context or the repository, then null is returned.
        /// </summary>
        /// <param name="oid">A key uniquelly  identifying document to find.</param>
        /// <returns>A document entry with the given oid key; or null if such document is not found.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the context has been disposed.</exception>
        public override async Task<NDocumentEntry> FindDocumentAsync(ObjectId oid)
        {
            var doc = (await this.FindAsync(oid));
            return Context.Entry(doc);
        }

        /// <summary>
        /// Casts the current <see cref="NDocumentSet"/> to <see cref="NDocumentSet{TTargetDocument}"/> if the current object contains elements of the required type. 
        /// </summary>
        /// <typeparam name="TTargetDocument">A type of elements.</typeparam>
        /// <returns>The typed version of <see cref="NDocumentSet{TTargetDocument}"/> if the current <see cref="NDocumentSet"/> can contain elements of the required type.</returns>
        public override NDocumentSet<TTargetDocument> Cast<TTargetDocument>() => this as NDocumentSet<TTargetDocument>;

        /// <summary>
        ///     Counts the total entities in the repository.
        /// </summary>
        /// <returns>Count of entities in the collection.</returns>
        public override async Task<long> CountAsync()
        {
            return await MongoCollection.CountAsync(new BsonDocument()).ConfigureAwait(false);
        }

        public override NDocumentEntry AddDocument(object document)
        {
            var doc = document as TDocument;
            return doc != null ? this.Add(doc) : null;
        }

        public override NDocumentEntry AttachDocument(object document)
        {
            var doc = document as TDocument;
            return doc != null ? this.Attach(doc) : null;
        }

        public override NDocumentEntry RemoveDocument(object document)
        {
            var doc = document as TDocument;
            return doc != null ? this.Remove(doc) : null;
        }

        public override NDocumentEntry UpdateDocument(object document)
        {
            var doc = document as TDocument;
            return doc != null ? this.Update(doc) : null;
        }

        /// <summary>
        ///     Finds a document with the given oid key. If a document with the given oid exists in the context,
        ///     then it is returned immediately without making a request to the store. Otherwise, a request is made to the
        ///     repository for
        ///     a document with the given oid and this document, if found, is attached to the context and returned.
        ///     If no document is found in the context or the repository, then null is returned.
        /// </summary>
        /// <param name="oid">A key uniquelly  identifying document to find.</param>
        /// <returns>A document with the given oid key; or null if such document is not found.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the context has been disposed.</exception>
        public virtual async Task<TDocument> FindAsync(ObjectId oid)
        {
            return await MongoCollection.FindOneByIdAsync(oid);
        }
        /// <summary>
        /// Gets the collection of documents that matches the specified filter.
        /// </summary>
        /// <param name="filter">An expression tree that represents a filter.</param>
        /// <param name="options">The options to refine the find operation.</param>
        /// <param name="cancellationToken">The cancellation token object.</param>
        /// <returns>The collection of documents that matches the specified filter.</returns>
        public virtual async Task<IEnumerable<TDocument>> FindAsync(Expression<Func<TDocument, bool>> filter = null,
            FindOptions<TDocument> options = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            var result = new List<TDocument>();
            try
            {
                using (
                    var cursor =
                        await
                            MongoCollection.FindAsync(filter ?? Filter.And(new BsonDocument()), options,
                                cancellationToken))
                {
                    while (await cursor.MoveNextAsync(cancellationToken))
                    {
                        var batch = cursor.Current;
                        foreach (var doc in batch)
                        {
                            System.Diagnostics.Debug.Assert(doc != null,"null object retrieved from the cursor enumeration");
                            result.Add(doc);
                        }
                    }
                }
            }
            catch(TaskCanceledException)
            {
                // ignored
            }
            return result;
        }
        /// <summary>
        /// Gets the first document that matches the specified filter.
        /// </summary>
        /// <param name="filter">An expression tree that represents a filter.</param>
        /// <param name="cancellationToken">The cancellation token object.</param>
        /// <returns>The first document that matches the specified filter; or <c>null</c> if no document matches the filter.</returns>
        public virtual async Task<TDocument> FindOneAsync(Expression<Func<TDocument, bool>> filter,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var result =
                await FindAsync(filter, new FindOptions<TDocument> {Limit = 1}, cancellationToken).ConfigureAwait(false);
            return result.FirstOrDefault();
        }

        /// <summary>
        ///     Removes the given document from the from the underlying data store.
        /// </summary>
        /// <param name="document">The document to delete.</param>
        /// <param name="cancellationToken">The cancellation token object.</param>
        /// <returns>The deleted document if operation completed sucessuly; otherwise <c>null</c>.</returns>
        public virtual async Task<TDocument> DeleteAsync(TDocument document, CancellationToken cancellationToken = default(CancellationToken))
        {
            var id = ((IDocument) document).Id;
            var filter = Filter.Eq("_id", id);
            var result = await MongoCollection.DeleteOneAsync(filter, cancellationToken).ConfigureAwait(false);
            return result.IsAcknowledged ? document : default(TDocument);
        }
        /// <summary>
        /// Deletes documents that matches the given filter from the underlying data store.
        /// </summary>
        /// <param name="filter">An expression tree that represents a filter.</param>
        ///  <param name="cancellationToken">The cancellation token object.</param>
        /// <returns>Number of document deelted.</returns>
        public virtual async Task<long> DeleteAsync(Expression<Func<TDocument, bool>> filter, CancellationToken cancellationToken = default(CancellationToken))
        {
            var deleteResult = await MongoCollection.DeleteManyAsync(filter, cancellationToken);
            return (deleteResult.IsAcknowledged) ? deleteResult.DeletedCount : 0;
        }
        /// <summary>
        /// Deletes all documents in the current set from the underlying data store.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token object.</param>
        /// <returns>Number of document deelted.</returns>
        public virtual async Task<long> DeleteAllAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            var deleteResult = await MongoCollection.DeleteManyAsync(new BsonDocument(), cancellationToken).ConfigureAwait(false); ;
            return (deleteResult.IsAcknowledged) ? deleteResult.DeletedCount : 0;
        }

        #endregion

        #region IEnumerable implementation

        /// <summary>
        ///  Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>An enumerator that can be used to iterate through the collection.</returns>
        IEnumerator<TDocument> IEnumerable<TDocument>.GetEnumerator()
        {
            var result = (Task.Run(() => FindAsync())).Result;
            return result.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        /// <summary>
        ///  Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>An enumerator that can be used to iterate through the collection.</returns>
        public override IEnumerator GetEnumerator()
        {
            var result = (Task.Run(() => FindAsync())).Result;
            return result.GetEnumerator();
        }

        #endregion
    }
}