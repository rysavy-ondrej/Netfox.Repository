//
//  NPropertyEntry.cs
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

namespace Netfox.Repository
{
    /// <summary>
    ///     Instances of this class are returned from the Property method of NDocumentEntry.
    /// </summary>
    /// <typeparam name="TDocument">The type of the document to which this property belongs.</typeparam>
    /// <typeparam name="TProperty">The type of the property.</typeparam>
    public class NPropertyEntry<TDocument, TProperty>
        where TDocument : class, new()
    {
        /// <summary>
        ///     Gets NDocumentEntry to which this
        ///     colection navigation property belongs.
        /// </summary>
        public NDocumentEntry<TDocument> DocumentEntry { get; }

        /// <summary>
        ///     Gets the property name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        ///     Creates a new <see cref="NPropertyEntry{TDocument,TProperty}" />  object that
        ///     provides meta-information about controlled property of the document.
        /// </summary>
        /// <param name="documentEntry">The document entry object.</param>
        /// <param name="propertyName">The property name.</param>
        internal NPropertyEntry(NDocumentEntry<TDocument> documentEntry, string propertyName)
        {
            DocumentEntry = documentEntry;
            Name = propertyName;
        }

        /// <summary>
        ///     Gets or sets the current value of the navigation property.
        /// </summary>
        public TProperty CurrentValue
        {
            get { return (TProperty) (NDocumentEntry<TDocument>.DocumentAccessor[DocumentEntry.Document, Name]); }
            set { (NDocumentEntry<TDocument>.DocumentAccessor[DocumentEntry.Document, Name]) = value; }
        }
    }
}