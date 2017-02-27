//
//  NDocumentStateEntry.cs
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
using System.Runtime.CompilerServices;
using MongoDB.Bson;

#endregion

namespace Netfox.Repository.Internals
{
    /// <summary>
    ///     Tracks the state of the document. For each tracked document in <see cref="NDocumentStateManager" /> a single
    ///     <see cref="NDocumentStateEntry" /> exists.
    /// </summary>
    /// <remarks>
    ///     If document is in unchanged state than it is referenced using a weak-reference so we can detect if the document is
    ///     no longer used.
    /// </remarks>
    internal class NDocumentStateEntry
    {
        #region Private fields - the only state variables of the class

        // TODO (NDocumentStateEntry._modifiedProperties): replace with some more memory-friendly type.
        private HashSet<string> _modifiedProperties;
        private IDocumentWrapper _wrappedDocument;

        #endregion

        /// <summary>
        ///     Creates NDocumentStateEntry that tracks the document in the wrapper.
        /// </summary>
        /// <param name="wrapper">A wrapper that contains the document to be tracked.</param>
        /// <param name="state">An initial state of the tracked document.</param>
        internal NDocumentStateEntry(IDocumentWrapper wrapper, DocumentState state)
        {
            State = state;
            _wrappedDocument = wrapper;
            Debug.Assert(wrapper.DocumentType != typeof (object));
        }

        /// <summary>
        ///     Gets the state of the tracked document.
        /// </summary>
        internal DocumentState State { get; private set; }


        /// <summary>
        ///     Gets the tracked document. It can return null if  the document state is <see cref="DocumentState.Unchanged" /> and
        ///     the tracked document was reclaimed by GC.
        /// </summary>
        internal object Document
        {
            get
            {
                ValidateState();
                return _wrappedDocument.Document;
            }
        }

        /// <summary>
        ///     Gets the object Id. This value is available even if the tracked document was collected.
        /// </summary>
        internal ObjectId Key => _wrappedDocument.DocumentId;

        /// <summary>
        ///     Gets the collection name to which the referenced object belongs.
        /// </summary>
        internal string CollectionName => _wrappedDocument.CollectionName;

        /// <summary>
        ///     Gets the type of documnet tracked by the current State Entry object.
        /// </summary>
        internal Type DocumentType => _wrappedDocument.DocumentType;

        /// <summary>
        ///     Changes the state of the tracked document.
        /// </summary>
        /// <param name="state">A new state of the document.</param>
        /// <returns>
        ///     true if document has the requested <see cref="DocumentState" /> value; false if one attempts to change the status
        ///     of a dead document entry.
        /// </returns>
        /// <remarks>
        ///     If the state is changed to/from <see cref="DocumentState.Unchanged" />
        ///     it amounts to create a new DocumentWrapper. It may be possible that
        ///     <see cref="DocumentState" /> cannot be changed. This case only occurs if one attempts to change the status
        ///     of a dead document entry.
        /// </remarks>
        internal bool ChangeState(DocumentState state)
        {
            var document = Document;
            if (document == null) return false;
            if (State == state) return true;

            // if we are changing the state to Unchanged we must ensure that WeakWrapper is in use:
            if (state == DocumentState.Unchanged)
            {
                if (_wrappedDocument is StrongDocumentWrapper)
                    _wrappedDocument = DocumentWrapperFactory.CreateNewWrapper(document, Key, CollectionName, state);
                _modifiedProperties?.Clear();
            }
            else
            {
                if (_wrappedDocument is WeakDocumentWrapper)
                    _wrappedDocument = DocumentWrapperFactory.CreateNewWrapper(document, Key, CollectionName, state);
            }
            State = state;
            GC.KeepAlive(document);
            return true;
        }

        /// <summary>
        ///     Tests if the property of the tracked document was changed.
        /// </summary>
        /// <param name="propertyName">A property name of the document.</param>
        /// <returns>true if the property was changed; false otherwise.</returns>
        internal bool IsPropertyChanged(string propertyName)
        {
            ValidateState();
            return _modifiedProperties?.Contains(propertyName) ?? false;
        }

        /// <summary>
        ///     Test if the state of the current document is valid for performing operations of document state entry.
        /// </summary>
        /// <param name="methodName">Name of the method that called the validation.</param>
        private void ValidateState([CallerMemberName] string methodName = "")
        {
            if (State == DocumentState.Detached)
            {
                throw new InvalidOperationException(
                    $"DocumentStateEntry is in DetachedState which is invalid in {methodName} context.");
            }
        }

        /// <summary>
        ///     Callback used by <see cref="NDocumentStateManager" /> to inform the tracker that a property of the tracked object
        ///     was changed.
        /// </summary>
        /// <param name="propertyChangedEventArgs">Arguments of the callback.</param>
        internal void PropertyChanged(PropertyChangedEventArgs propertyChangedEventArgs)
        {
            if (_modifiedProperties == null) _modifiedProperties = new HashSet<string>();
            _modifiedProperties.Add(propertyChangedEventArgs.PropertyName);
        }

        /// <summary>
        ///     Sets the new document wrapper associated with the current <see cref="NDocumentStateEntry" /> instance.
        /// </summary>
        /// <param name="wrapper">A new document wrapper to be used by the current object.</param>
        /// <remarks>
        ///     This method is supposed to be used only for resurrection of the document when
        ///     <see cref="WeakDocumentWrapper" /> wrapper is used for referencing to the document.
        /// </remarks>
        internal void SetWrapper(IDocumentWrapper wrapper)
        {
            _wrappedDocument = wrapper;
        }
    }
}