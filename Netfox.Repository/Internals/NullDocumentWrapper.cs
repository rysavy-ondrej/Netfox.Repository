//
//  NullDocumentWrapper.cs
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
using MongoDB.Bson;

#endregion

namespace Netfox.Repository.Internals
{
    /// <summary>
    ///     This class implements a null wrapper. This wrappede does not hold any document and can be used
    ///     in a special situations. The class is implements singleton pattern. Use <see cref="NullWrapper" />
    ///     property to get its only instance.
    /// </summary>
    internal class NullDocumentWrapper : IDocumentWrapper
    {
        /// <summary>
        ///     Stores a singleton of <see cref="NullDocumentWrapper" /> class.
        /// </summary>
        private static readonly Lazy<NullDocumentWrapper> _nullWrapper =
            new Lazy<NullDocumentWrapper>(() => new NullDocumentWrapper());

        /// <summary>
        ///     Gets the only instance of <see cref="NullDocumentWrapper" /> class.
        /// </summary>
        public static IDocumentWrapper NullWrapper => _nullWrapper.Value;

        /// <summary>
        ///     Always returns <code>false</code>.
        /// </summary>
        public bool IsAlive => false;
        /// <summary>
        /// Always returns <code>null</code> value.
        /// </summary>
        public string CollectionName {
            get { return null; }
            set { }
        }

        /// <summary>
        ///     Always returns <code>null</code> as <see cref="NullDocumentWrapper" /> has never associated any document object.
        /// </summary>
        public object Document => null;

        /// <summary>
        ///     Always returns <code>typeof(object)</code>.
        /// </summary>
        public Type DocumentType => typeof (object);

        /// <summary>
        ///     Always returns <see cref="ObjectId.Empty" />
        /// </summary>
        public ObjectId DocumentId
        {
            get { return ObjectId.Empty; }
            set { }
        }
    }
}