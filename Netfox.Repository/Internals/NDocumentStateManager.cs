//
//  NDocumentStateManager.cs
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
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using JetBrains.Annotations;
using MongoDB.Bson;
using Netfox.Repository.Utils;

#endregion

namespace Netfox.Repository.Internals
{
    /// <summary>
    ///     Manages the state and identity of document objects. It provides caching and identity resolution capability.
    /// </summary>
    /// <remarks>
    ///     This class implements a policy that provides document caching and ensures that only single instance of a document
    ///     exists in contexts that share the <see cref="NDocumentStateManager" /> instance.
    ///     <see cref="NDocumentStateManager" /> maintains various dictionaries of <see cref="NDocumentStateEntry" /> objects.
    ///     These state entry objects track states of document objects. Possible states are provided by
    ///     <see cref="DocumentState" />
    ///     enumeration.
    /// </remarks>
    public class NDocumentStateManager : IDisposable
    {
        /// <summary>
        ///     Lock used to control the access to dictionaries of the <see cref="NDocumentStateManager" />.
        /// </summary>
        /// <remarks>
        /// This lock is used in publicly available methods. Private methods assumes that the access control 
        /// is ensured by the caller.
        /// </remarks>
        private readonly ReaderWriterLockSlim _entityStoresLock =
            new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);

        /// <summary>
        ///     This dictionary stored entities that was added to the context but not written to the database.
        /// </summary>
        private readonly Dictionary<ObjectId, NDocumentStateEntry> _addedEntityStore =
            new Dictionary<ObjectId, NDocumentStateEntry>();

        /// <summary>
        ///     This dictionary stores entitites that was deleted in the context but not removed from the database.
        /// </summary>
        private readonly Dictionary<ObjectId, NDocumentStateEntry> _deletedEntityStore =
            new Dictionary<ObjectId, NDocumentStateEntry>();

        /// <summary>
        ///     This dictionary stores entities that was modified but these modifications were not persisted to the database.
        /// </summary>
        private readonly Dictionary<ObjectId, NDocumentStateEntry> _modifiedEntityStore =
            new Dictionary<ObjectId, NDocumentStateEntry>();

        /// <summary>
        ///     This dictionary contains entities that are identical to the data stored in the database. In other words,
        ///     this is object cache. Objects are referenced using WeakReference so thet GC can remove it.
        /// </summary>
        private readonly NDocumentCache<ObjectId, NDocumentStateEntry> _unchangedEntityStore =
            new NDocumentCache<ObjectId, NDocumentStateEntry>((key, entry) => entry.Document == null);

        /// <summary>
        ///     This set contains ids of objects for which property-change tracking is temporally switched off.
        ///     Usually, this set contains objects currently read from the database.
        /// </summary>
        private readonly HashSet<ObjectId> _suppressChangeTrackingSet = new HashSet<ObjectId>();

        #region Counters and status indicators

        /// <summary>
        ///     Gets the time of the last clean up.
        /// </summary>
        internal DateTime LastCleanUp { get; private set; }

        /// <summary>
        ///     Counts the total number of reclaimed dead document entries.
        /// </summary>
        internal long TotalReclaimedDocuments { get; private set; }

        /// <summary>
        ///     Provides information on total running time spend on cleaning the document cache.
        /// </summary>
        internal TimeSpan TotalCleanupTime { get; private set; }

        /// <summary>
        ///     Gets the number of executed full cleanup procedures.
        /// </summary>
        internal long FullCleanUpExecCount { get; private set; }

        /// <summary>
        ///     Gets the number of executed partial cleanup procedures.
        /// </summary>
        internal long PartialCleanUpExecCount { get; private set; }

        #endregion

        /// <summary>
        ///     Gets or sets the the percent of documents affected by the partial cleanup. Default is set to 10%.
        /// </summary>
        internal int PartialCleanUpPercent { get; set; }

        /// <summary>
        /// Creates a new instance of <see cref="NDocumentStateManager"/>.
        /// </summary>
        internal NDocumentStateManager()
        {
            PartialCleanUpPercent = 10;
        }


        /// <summary>
        ///     Calling this method performs cleaning the state manager internal resources. It means that all dead documents
        ///     will be removed from the cache.
        /// </summary>
        /// <param name="fullCleanUp">
        ///     Set to true if the full clean up is required. False means that only incremental clean up will
        ///     be perfomed.
        /// </param>
        /// <remarks>
        ///     Full cleanup means that all reclamable documents will be removed from the cache.
        ///     Partial clean up means that only portion of reclamable documents will be removed. The number of document
        ///     documents removed during partial cleanup is determined by <see cref="PartialCleanUpPercent" /> value.
        /// </remarks>
        public void CleanUp(bool fullCleanUp = true)
        {
            this._entityStoresLock.EnterWriteLock();
            try
            {
                var sw = Stopwatch.StartNew();
                if (fullCleanUp) FullCleanUpExecCount ++;
                else PartialCleanUpExecCount ++;
                var count = _RemoveDeadDocumentStateEntries(fullCleanUp ? int.MaxValue : PartialCleanUpPercent);
                TotalReclaimedDocuments += count;
                TotalCleanupTime += sw.Elapsed;
                LastCleanUp = DateTime.Now;
            }
            finally
            { this._entityStoresLock.ExitWriteLock();}
        }

        /// <summary>
        ///     This methods ensures that the document is tracked by the <see cref="NDocumentStateManager" />.
        ///     If document is already tracked it does nothing and its current <see cref="NDocumentStateEntry" /> object is returned.
        ///     If document is not tracked then it is added with the specified state.
        ///     If <see cref="NDocumentStateEntry" /> object for the specified <paramref name="key" /> is found but the document object is different
        ///     (or null) it is updated.
        /// </summary>
        /// <param name="key">An instance of <see cref="ObjectId" /> that represents document key.</param>
        /// <param name="documentType">A type of the document.</param>
        /// <param name="document">A document object to be tracked by the current <see cref="NDocumentStateManager" />.</param>
        /// <param name="documentState">A <see cref="DocumentState" /> value used as the initial state for newly create <see cref="NDocumentStateEntry"/>.</param>
        /// <returns><see cref="NDocumentStateEntry" /> instance associated with the tracked document.</returns>
        /// <remarks>
        ///     After calling this method the caller can be sure that returned <see cref="NDocumentStateEntry" /> defines tracked
        ///     document
        ///     as identified by <paramref name="key" /> value and that
        ///     <code><see cref="NDocumentStateEntry.Document" />==<paramref name="document" /></code>.
        ///     When using this method, caller should guarantee the consistency between <paramref name="key" /> and
        ///     <paramref name="document" /> values. If
        ///     <see cref="NDocumentStateEntry" /> exists the method updates the document in <see cref="NDocumentStateEntry" />
        ///     with specified <paramref name="key" /> to refer to <paramref name="document" />.
        /// </remarks>
        internal NDocumentStateEntry AddOrGetExistingStateEntry(ObjectId key, [NotNull] Type documentType,
            [NotNull] object document, DocumentState documentState = DocumentState.Unchanged)
        {
            if (documentType == null) throw new ArgumentNullException(nameof(documentType));
            if (document == null) throw new ArgumentNullException(nameof(document));
            this._entityStoresLock.EnterUpgradeableReadLock();
            try
            {
                NDocumentStateEntry documentStateEntry;
                if (_TryGetDocumentStateEntry(key, out documentStateEntry))
                {
                    if (documentStateEntry.Document == document)
                    {
                        return documentStateEntry;
                    }
                    else  // needs to update entry found with the provided object keeping its current state
                    {
                        var wrapper = DocumentWrapperFactory.CreateNewWrapper(document, key, documentType.Name, documentStateEntry.State);
                        documentStateEntry.SetWrapper(wrapper);
                    }
                }
                else
                {   // create a new entry here
                    this._entityStoresLock.EnterWriteLock();
                    try
                    {
                        var wrapper = DocumentWrapperFactory.CreateNewWrapper(document, key, documentType.Name, documentState);
                        documentStateEntry = _AddDocumentStateEntry(key, wrapper, documentState);
                    }
                    finally
                    {
                        this._entityStoresLock.ExitWriteLock();
                    }
                }
                return documentStateEntry;
            }
            finally
            {
                this._entityStoresLock.ExitUpgradeableReadLock();
            }            
        }

        /// <summary>
        ///     Gets an enumeration of <see cref="NDocumentStateEntry" /> objects that are in one of the given
        ///     <see cref="DocumentState" />.
        /// </summary>
        /// <param name="state">The <see cref="DocumentState" /> value/</param>
        /// <returns>An enumeration of <see cref="NDocumentStateEntry" /> that are in one of the given state.</returns>
        /// <remarks>
        ///     Note that using this method with <see cref="DocumentState.Unchanged" /> argument is costly. It is because
        ///     the state maneger must enumerate all entries in the unchagned document cache to detect dead entries.
        /// </remarks>
        internal IEnumerable<NDocumentStateEntry> GetDocumentStateEntries(DocumentState state)
        {
            if (state.HasFlag(DocumentState.Detached))
            {
                throw new ArgumentException("Detached objects cannot be obtained from the DocumentStateManager.",
                    nameof(state));
            }
            this._entityStoresLock.EnterReadLock();
            try
            {
                return _GetDocumentStateEntries(state);
            }
            finally
            { this._entityStoresLock.ExitReadLock();}
        }

        /// <summary>
        ///     Changes the state of the managed document. By changing the document state
        ///     the <see cref="NDocumentStateEntry" /> is removed from source collection and inserted to target collection.
        ///     Also <see cref="NDocumentStateEntry.ChangeState" /> method is called to change the state of the
        ///     <see cref="NDocumentStateEntry" /> itself.
        /// </summary>
        /// <param name="entry">A <see cref="NDocumentStateEntry" /> object whose state is to be changed.</param>
        /// <param name="documentState">The target <see cref="DocumentState" /> value applied to the document.</param>
        /// <returns><see cref="NDocumentStateEntry" /> object on success; otherwise, <c>null</c>.</returns>
        internal NDocumentStateEntry ChangeDocumentState(NDocumentStateEntry entry, DocumentState documentState)
        {
            if (entry == null || entry.State == documentState) return entry;
            if (entry.Document == null) return null;

            this._entityStoresLock.EnterWriteLock();
            try
            {
                var document = entry.Document;
                if (document == null) return null;

                if (documentState != DocumentState.Detached)
                {
                    _RemoveDocumentStateEntryFromDictionary(entry, entry.State);
                }

                if (!entry.ChangeState(documentState)) return null;

                if (documentState != DocumentState.Detached)
                {
                    _AddDocumentStateEntryToDictionary(entry, documentState);
                }

                GC.KeepAlive(document);
                return entry;
            }
            finally
            {
                this._entityStoresLock.ExitWriteLock();
            }
        }

        /// <summary>
        ///     Called when the observed property on managed object was changed. It causes that
        ///     the state of this document will be modified accordingly and the modified property
        ///     will be recorded with the existing <see cref="NDocumentStateEntry" /> object.
        /// </summary>
        /// <param name="sender">The sender object of the event. This is always a document object.</param>
        /// <param name="propertyChangedEventArgs">
        ///     A <see cref="PropertyChangedEventArgs" /> instance containing information about
        ///     the event.
        /// </param>
        internal void OnDocumentControlledPropertyChanged(object sender,
            PropertyChangedEventArgs propertyChangedEventArgs)
        {
            var document = sender as IDocument;
            // if invalid object is presented or the documen change tracking is disabled 
            // then we silently leave this method.
            if (document == null || _suppressChangeTrackingSet.Contains(document.Id)) return;

            NDocumentStateEntry entry;
            // if the document is not tracked just return:
            if (!TryGetDocumentStateEntry(document.Id, out entry)) return;

            entry.PropertyChanged(propertyChangedEventArgs);
            // replace unchanged state with modified state; remain other states unaffected
            if (entry.State.HasFlag(DocumentState.Unchanged))
                ChangeDocumentState(entry, DocumentState.Modified);
            GC.KeepAlive(document);
        }

        /// <summary>
        ///     Enables or disables tracking changes of the document properties.
        /// </summary>
        /// <param name="key"><see cref="ObjectId" /> value identifying the target document.</param>
        /// <param name="enableTracking">
        ///     true if property changes should be tracked; false if property changes should not be
        ///     tracked.
        /// </param>
        internal void SetDocumentPropertyTracking(ObjectId key, bool enableTracking)
        {

            _entityStoresLock.EnterUpgradeableReadLock();
            try
            {
                NDocumentStateEntry entry;
                if (_TryGetDocumentStateEntry(key, out entry))
                {
                    _entityStoresLock.EnterWriteLock();
                    try
                    {
                        if (enableTracking)
                        {
                            _EnableChangeTracking(entry);
                        }
                        else
                        {
                            _DisableChangeTracking(entry);
                        }
                    }
                    finally
                    {
                        _entityStoresLock.ExitWriteLock();
                    }
                }
            }
            finally
            {
                _entityStoresLock.ExitUpgradeableReadLock();
            }
        }

        private void _DisableChangeTracking(NDocumentStateEntry entry)
        {
            _suppressChangeTrackingSet.Add(entry.Key);
        }

        private void _EnableChangeTracking(NDocumentStateEntry entry)
        {
            _suppressChangeTrackingSet.Remove(entry.Key);
        }

        /// <summary>
        ///     Gets the number of managed object for the given DocumentState.
        /// </summary>
        /// <param name="state">DocumentState value for which the number of documents to be provided.</param>
        /// <returns>The number of managed object for the given DocumentState.</returns>
        /// <remarks>
        ///     Note that documents in DocumentState.Added, DocumentState.Modified, DocumentState.Deleted and
        ///     DocumentState.Unchanged
        ///     states are managed by the DocumentStateManager. If other state is specified then the return value will always be
        ///     zero.
        /// </remarks>
        internal int GetDocumentStateEntriesCount(DocumentState state)
        {
            _entityStoresLock.EnterReadLock();
            try
            {
                var num = 0;
            if (state.HasFlag(DocumentState.Added))
            {
                num += _addedEntityStore?.Count ?? 0;
            }
            if (state.HasFlag(DocumentState.Modified))
            {
                num += _modifiedEntityStore?.Count ?? 0;
            }
            if (state.HasFlag(DocumentState.Deleted))
            {
                num += _deletedEntityStore?.Count ?? 0;
            }
            if (state.HasFlag(DocumentState.Unchanged))
            {
                num += _unchangedEntityStore?.GetApproximateCount() ?? 0;
            }
            return num;
            }
            finally
            {
                _entityStoresLock.ExitReadLock();
            }
        }

        /// <summary>
        ///     Gets document state entry if one exists for the given document.
        /// </summary>
        /// <param name="key">Identification of the document for which the document state entry to be provided.</param>
        /// <param name="entry">A NDocumentStateEntry instance for document object if exists; null otherwise.</param>
        /// <returns>true if document state entry exists; false otherwise.</returns>
        internal bool TryGetDocumentStateEntry(ObjectId key, out NDocumentStateEntry entry)
        {
            _entityStoresLock.EnterReadLock();
            try
            {
                return _TryGetDocumentStateEntry(key, out entry);
            }
            finally
            {
                _entityStoresLock.ExitReadLock();
            }
        }



        /// <summary>
        ///     Gets document state entry if one exists for the given document.
        /// </summary>
        /// <param name="document">A document object for which the document state entry to be get.</param>
        /// <param name="entry">A NDocumentStateEntry instance for document object if exists; null otherwise.</param>
        /// <returns>true if document state entry exists; false otherwise.</returns>
        internal bool TryGetDocumentStateEntry(object document, out NDocumentStateEntry entry)
        {
            var key = (document as IDocument)?.Id ?? ObjectId.Empty;
            _entityStoresLock.EnterReadLock();
            try
            {
                return _TryGetDocumentStateEntry(key, out entry);
            }
            finally
            {
                _entityStoresLock.ExitReadLock();
            }
        }

        #region Private methods for maintaing document state entry collections

        /// <summary>
        ///     Adds a new document to the manager. This is internal method. It assumes that the caller
        ///     has already acquired WriteLock.
        /// </summary>
        /// <param name="key">Id value of the document.</param>
        /// <param name="wrapper">A document wrapper that holds the document to be added.</param>
        /// <param name="state">An initial state of the document. Note that it must not be DocumentState.Detached.</param>
        /// <return>NDocumentstateEntry created for the document.</return>
        private NDocumentStateEntry _AddDocumentStateEntry(ObjectId key, IDocumentWrapper wrapper, DocumentState state)
        {
            var document = wrapper.Document as IDocument;
            if (document == null) return null;

            var entry = new NDocumentStateEntry(wrapper, state);
            document.ControlledPropertyChanged += OnDocumentControlledPropertyChanged;
            _AddDocumentStateEntryToDictionary(entry, state);
            return entry;
        }

        /// <summary>
        ///     Adds <see cref="NDocumentStateEntry" /> to the managed entry collection depending on the specified
        ///     <paramref name="state" />.
        /// </summary>
        /// <param name="entry">A <see cref="NDocumentStateEntry" /> object managing the state of the document.</param>
        /// <param name="state">A <see cref="DocumentState" /> value representing the initial tracking state of the document.</param>
        /// <remarks>
        ///     The <see cref="NDocumentStateEntry" />  object is added to collection depending on the specified
        ///     <see cref="DocumentState" />:
        ///     <list type="table">
        ///         <listheader>
        ///             <term>Document state</term>
        ///             <description>Target collection</description>
        ///         </listheader>
        ///         <item>
        ///             <term>
        ///                 <see cref="DocumentState.Added" />
        ///             </term>
        ///             <description>
        ///                 <see cref="_addedEntityStore" />
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <term>
        ///                 <see cref="DocumentState.Deleted" />
        ///             </term>
        ///             <description>
        ///                 <see cref="_deletedEntityStore" />
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <term>
        ///                 <see cref="DocumentState.Modified" />
        ///             </term>
        ///             <description>
        ///                 <see cref="_modifiedEntityStore" />
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <term>
        ///                 <see cref="DocumentState.Unchanged" />
        ///             </term>
        ///             <description>
        ///                 <see cref="_unchangedEntityStore" />
        ///             </description>
        ///         </item>
        ///     </list>
        /// </remarks>
        private void _AddDocumentStateEntryToDictionary(NDocumentStateEntry entry, DocumentState state)
        {

            IDictionary<ObjectId, NDocumentStateEntry> dictionary = null;

                if (state == DocumentState.Unchanged)
                {
                    _unchangedEntityStore.Set(entry.Key, entry);
                }
                else
                {
                    switch (state)
                    {
                        case DocumentState.Added:
                            dictionary = _addedEntityStore;
                            break;
                        case DocumentState.Deleted:
                            dictionary = _deletedEntityStore;
                            break;
                        case DocumentState.Detached:
                            throw new ArgumentException(
                                "Dcouments in detached state could not be added to document state manager.",
                                nameof(state));
                        case DocumentState.Modified:
                            dictionary = _modifiedEntityStore;
                            break;
                    }
                    if (dictionary != null) dictionary[entry.Key] = entry;
                }

        }

        /// <summary>
        ///     Tries to remove the <see cref="NDocumentStateEntry" /> from the collection by the specified
        ///     <see cref="DocumentState" />.
        /// </summary>
        /// <param name="entry">An <see cref="NDocumentStateEntry" /> object to be removed.</param>
        /// <param name="state"><see cref="DocumentState" /> value to selected target collection.</param>
        /// <returns>The entry object which was removed from the dictionary or null if nothing was removed.</returns>
        private NDocumentStateEntry _RemoveDocumentStateEntryFromDictionary(NDocumentStateEntry entry,
            DocumentState state)
        {

                if (state == DocumentState.Unchanged)
                {
                    return _unchangedEntityStore.Remove(entry.Key);
                }
                else
                {
                    IDictionary<ObjectId, NDocumentStateEntry> dictionary = null;
                    switch (state)
                    {
                        case DocumentState.Added:
                            dictionary = _addedEntityStore;
                            break;
                        case DocumentState.Deleted:
                            dictionary = _deletedEntityStore;
                            break;
                        case DocumentState.Detached:
                            throw new ArgumentException(
                                "Document in detached state could not be added to document state manager.",
                                nameof(state));
                        case DocumentState.Modified:
                            dictionary = _modifiedEntityStore;
                            break;
                    }
                    return (dictionary?.Remove(entry.Key) ?? false) ? entry : null;
                }
 
        }

        /// <summary>
        ///     Gets document state entry if one exists for the given document represented by its Id.
        /// </summary>
        /// <param name="key">A document Id for which the document state entry to be get.</param>
        /// <param name="entry">A NDocumentStateEntry instance for document object if exists; null otherwise.</param>
        /// <returns>true if document state entry exists; false otherwise.</returns>
        private bool _TryGetDocumentStateEntry(ObjectId key, out NDocumentStateEntry entry)
        {
            entry = null;
            return (_addedEntityStore?.TryGetValue(key, out entry) ?? false)
                   || (_unchangedEntityStore?.TryGetValue(key, out entry) ?? false)
                   || (_modifiedEntityStore?.TryGetValue(key, out entry) ?? false)
                   || (_deletedEntityStore?.TryGetValue(key, out entry) ?? false);
        }

        /// <summary>
        ///     Gets an array of <see cref="NDocumentStateEntry" /> objects that have the specified <see cref="DocumentState" />
        ///     <paramref name="state" />.
        /// </summary>
        /// <param name="state">A <see cref="DocumentState" /> value specifying document state.</param>
        /// <returns>
        ///     An array of <see cref="NDocumentStateEntry" /> objects that have the specified <see cref="DocumentState" />.
        ///     If no document is in the specified state, then the array has zero length.
        /// </returns>
        private IEnumerable<NDocumentStateEntry> _GetDocumentStateEntries(DocumentState state)
        {
            List<NDocumentStateEntry> array = new List<NDocumentStateEntry>();
            
                if ((DocumentState.Added & state) != 0 && _addedEntityStore != null)
                {
                    array.AddRange(_addedEntityStore.Values);
                }
                if ((DocumentState.Modified & state) != 0 && _modifiedEntityStore != null)
                {
                    array.AddRange(_modifiedEntityStore.Values);
                }
                if ((DocumentState.Deleted & state) != 0 && _deletedEntityStore != null)
                {
                    array.AddRange(_deletedEntityStore.Values);
                }
                if ((DocumentState.Unchanged & state) != 0 && _unchangedEntityStore != null)
                {
                    array.AddRange(_unchangedEntityStore.Values);
                }
                return array;
        }

        /// <summary>
        ///     This method goes through the <see cref="_unchangedEntityStore" /> collection and removes those that
        ///     wrap dead documents.
        /// </summary>
        /// <param name="percent">The percentage of total cache entries to remove. This value must be between 0-100.</param>
        /// <returns>A number of dead documents removed from the manager.</returns>
        private int _RemoveDeadDocumentStateEntries(int percent)
        {
            var percentSafe = (percent > 0 && percent <= 100) ? percent : 10;
            var count = (double)_unchangedEntityStore.Capacity;
            var batchSize = (count/100)* percentSafe;
            return _unchangedEntityStore.Flush((int)batchSize);
        }

        #endregion

        #region IDisposable Support

        /// <summary>
        ///     This field is used by Dispose pattern to detect redundant calls.
        /// </summary>
        private bool _disposedValue;

        /// <summary>
        ///     Actually performs tasks associated with freeing and releasing resources used by NDocumentStateManage object on
        ///     behalf of IDisposable.Dispose method.
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _entityStoresLock?.Dispose();
                }
                _unchangedEntityStore.Clear();
                _addedEntityStore.Clear();
                _deletedEntityStore.Clear();
                _modifiedEntityStore.Clear();
                _disposedValue = true;
            }
        }

        /// <summary>
        ///     Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        #endregion

        #region Document related methods

        /// <summary>
        ///     Gets the state of the object specified by its <see cref="ObjectId" /> value.
        /// </summary>
        /// <param name="key"><see cref="ObjectId" /> value of the document to get information on it.</param>
        /// <returns>The <see cref="DocumentState" /> value of the object specified by its <see cref="ObjectId" /> value.</returns>
        public DocumentState GetDocumentState(ObjectId key)
        {
            NDocumentStateEntry documentStateEntry;
            return TryGetDocumentStateEntry(key, out documentStateEntry)
                ? documentStateEntry.State
                : DocumentState.Detached;
        }

        /// <summary>
        ///     Gets the document instance specified by its  <see cref="ObjectId" /> value.
        /// </summary>
        /// <param name="key"><see cref="ObjectId" /> value of the document to get information on it.</param>
        /// <returns>The document instance specified by its  <see cref="ObjectId" /> value</returns>
        public object GetDocument(ObjectId key)
        {
            NDocumentStateEntry documentStateEntry;
            return TryGetDocumentStateEntry(key, out documentStateEntry)
                ? documentStateEntry.Document
                : null;
        }

        /// <summary>
        ///     Gets the document type of the document specified by its <see cref="ObjectId" /> value.
        /// </summary>
        /// <param name="key"><see cref="ObjectId" /> value of the document to get information on it.</param>
        /// <returns>The document type of the document specified by its <see cref="ObjectId" /> value. </returns>
        public Type GetDocumentType(ObjectId key)
        {
            NDocumentStateEntry documentStateEntry;
            return TryGetDocumentStateEntry(key, out documentStateEntry)
                ? documentStateEntry.DocumentType
                : null;
        }

        /// <summary>
        ///     Gets the collection name of the document specified by its <see cref="ObjectId" /> value.
        /// </summary>
        /// <param name="key"><see cref="ObjectId" /> value of the document to get information on it.</param>
        /// <returns>The collection name of the document specified by its <see cref="ObjectId" /> value.</returns>
        public string GetCollectionName(ObjectId key)
        {
            NDocumentStateEntry documentStateEntry;
            return TryGetDocumentStateEntry(key, out documentStateEntry)
                ? documentStateEntry.CollectionName
                : null;
        }

        #endregion

        /// <summary>
        /// Gets the size of the unchanged document entry cache.
        /// </summary>
        /// <returns>The total size of the unchanged document entry cache incouding dead entries.</returns>
        public int GetDocumentCacheSize()
        {
            return this._unchangedEntityStore.Capacity;
        }
    }
}