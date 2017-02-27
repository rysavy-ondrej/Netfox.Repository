//
//  NCollectionEntry.cs
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
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using FastMember;
using MongoDB.Bson;

namespace Netfox.Repository
{
    /// <summary>
    /// Instances of this class are returned from the Collection method of NDocumentEntry
    /// and allow operations such as loading to be performed on the an document's collection navigation properties.
    /// </summary>
    /// <typeparam name="TDocument">The type of the document to which this property belongs.</typeparam>
    /// <typeparam name="TElement">The type of the element in the collection of entities.</typeparam>
    public class NCollectionEntry<TDocument, TElement> : ICollection<TElement>
        where TDocument : class, new()
        where TElement : class, new()
    {
        private static readonly TypeAccessor DocumentAccesor = TypeAccessor.Create(typeof (TDocument));

        /// <summary>
        /// Creates a new NCollectionEntry instance from the provided parameters.
        /// </summary>
        /// <param name="documentEntry">DocumentEntry object that describes the document.</param>
        /// <param name="name">A name of the navigable collection property.</param>
        internal NCollectionEntry(NDocumentEntry<TDocument> documentEntry, string name)
        {
            DocumentEntry = documentEntry;
            Name = name;
        }

        /// <summary>
        /// Gets NDocumentEntry to which this colection navigation property belongs.
        /// </summary>
        public NDocumentEntry<TDocument> DocumentEntry { get; }

        /// <summary>
        /// Gets the property name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets a value indicating whether all entities of this collection have been loaded from the database.
        /// </summary>
        /// <remarks>
        /// Collection is loaded if CurrentValue is not null. Collection is also considered loaded 
        /// if CurrentValue is null but there is not any record of referenced objects, i.e., it cannot be loaded from the database.
        /// </remarks>
        public bool IsLoaded => this.CurrentValue != null || this.ReferencedObjectIds == null;

        /// <summary>
        ///     Gets or sets the current value of the navigation property.
        ///     The current value is a collection of documents that this property references.
        /// </summary>
        public ICollection<TElement> CurrentValue
            => DocumentAccesor[DocumentEntry.Document, Name] as ICollection<TElement>;

        /// <summary>
        /// Adds a new object to the navigable collection.
        /// </summary>
        /// <param name="item">An object to add the the collection.</param>
        public void Add(TElement item)
        {
            CurrentValue.Add(item);
        }
        /// <summary>
        /// Clears entire collection. After this operation the collection will be empty.
        /// </summary>
        public void Clear()
        {
            CurrentValue.Clear();
        }
        /// <summary>
        /// Tests if the navigable collection contains the specified document.
        /// </summary>
        /// <param name="item">A document to find in the collection.</param>
        /// <returns>tur if the document is in the collection; false otherwise.</returns>
        public bool Contains(TElement item)
        {
            return CurrentValue.Contains(item);
        }
        /// <summary>
        /// Copies the content of the collection to the provided array.
        /// </summary>
        /// <param name="array">An array where the content of the collection will be copied.</param>
        /// <param name="arrayIndex">An index in the target array from which the content of the collection will be copied.</param>
        public void CopyTo(TElement[] array, int arrayIndex)
        {
            CurrentValue.CopyTo(array, arrayIndex);
        }

        /// <summary>
        /// Removes an element from navigable collection.
        /// </summary>
        /// <param name="item">Anelement to remove from the collection.</param>
        /// <returns>true if the element wa removed; false otherwise.</returns>
        public bool Remove(TElement item)
        {
            return CurrentValue.Remove(item);
        }
        /// <summary>
        /// Gets the enumerator object for the navigable collection.
        /// </summary>
        /// <returns>IEnumerator object that can be used to enumerate elements in the navugable collection.</returns>
        public IEnumerator<TElement> GetEnumerator()
        {
            return CurrentValue.GetEnumerator();
        }
        /// <summary>
        /// Gets the enumerator object for the navigable collection.
        /// </summary>
        /// <returns>IEnumerator object that can be used to enumerate elements in the navugable collection.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return CurrentValue.GetEnumerator();
        }
        /// <summary>
        /// Gets number of elements in navigable collection.
        /// </summary>
        public int Count => CurrentValue.Count;
        /// <summary>
        /// Gets true if the navigable collection is read only; false otherwise.
        /// </summary>
        public bool IsReadOnly => CurrentValue.IsReadOnly;

        /// <summary>
        /// Loads the collection of documents from the database. Note that documents that already exist
        /// in the context are not overwritten with values from the database.
        /// </summary>
        /// <remarks>
        /// Loads the navigable collection from the database. It only loads the collection if the property that stores
        /// the collection object is null.
        /// </remarks>
        public void Load()
        {
            if (this.CurrentValue == null)
            {
               Task.Run(() => DocumentEntry.LoadCollectionAsync(this)).Wait();
            }
        }
        /// <summary>
        /// Gets the array of ObjectId objects that represents a referenced collection of Documents.
        /// </summary>
        internal ObjectId[] ReferencedObjectIds => (ObjectId[]) ((DocumentEntry.Document as IDocument)?.Navigable[Name]);
    }
}