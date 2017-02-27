//
//  NReferenceEntry.cs
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

using System.Threading.Tasks;
using MongoDB.Bson;

#endregion

namespace Netfox.Repository
{
    /// <summary>
    ///     Instances of this class are returned from the Reference method of NDocumentEntry
    ///     and allow operations such as loading to be performed on the an document's reference navigation properties.
    /// </summary>
    /// <typeparam name="TDocument">The type of the document to which this property belongs.</typeparam>
    /// <typeparam name="TProperty">The type of the property.</typeparam>
    public class NReferenceEntry<TDocument, TProperty>
        where TDocument : class, new()
        where TProperty : class
    {
        internal NReferenceEntry(NDocumentEntry<TDocument> documentEntry, string propertyName)
        {
            DocumentEntry = documentEntry;
            Name = propertyName;
        }

        /// <summary>
        ///     Gets NDocumentEntry to which this navigation property belongs.
        /// </summary>
        public NDocumentEntry<TDocument> DocumentEntry { get; }

        /// <summary>
        ///     Gets a value indicating whether a referenced object have been loaded from the database and assigned to navigable
        ///     property.
        /// </summary>
        /// <remarks>
        ///     Navigable property is marked as loaded if it contains a refenerce to any object.
        ///     If it is null then is is considered as loaded if oid is empty or does not exist.
        /// </remarks>
        public bool IsLoaded => ReferencedObject != null || ObjectId.Empty.Equals(ReferencedObjectId);

        /// <summary>
        ///     Gets the property name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        ///     Gets or sets the current value of the navigation property.
        /// </summary>
        public TProperty CurrentValue
        {
            get { return ReferencedObject; }
            set { (NDocumentEntry<TDocument>.DocumentAccessor[DocumentEntry.Document, Name]) = value; }
        }

        internal ObjectId ReferencedObjectId
            => (ObjectId) ((DocumentEntry.Document as IDocument)?.Navigable[Name] ?? ObjectId.Empty);

        internal TProperty ReferencedObject
            => (NDocumentEntry<TDocument>.DocumentAccessor[DocumentEntry.Document, Name]) as TProperty;

        /// <summary>
        ///     Loads the document from the database. Note that if the document already exists in the context,
        ///     then it will not be overwritten with values from the database.
        /// </summary>
        /// <remarks>
        ///     This method violates async pattern. It wraps async operation with awaiter running at a separate thread.
        ///     In this context, the requirements on synchronous reading referenced objects is stronger than requirements of the
        ///     async pattern.
        /// </remarks>
        public void Load()
        {
            // nothing to load or already loaded...
            if ((ObjectId.Empty.Equals(ReferencedObjectId)) ||
                (ReferencedObjectId.Equals((ReferencedObject as IDocument)?.Id))) return;
            Task.Run(() => DocumentEntry.LoadReferenceAsync(this)).Wait();
        }
    }
}