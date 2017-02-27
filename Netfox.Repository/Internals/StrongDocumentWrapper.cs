//
//  StrongDocumentWrapper.cs
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
using System.Reflection;
using JetBrains.Annotations;
using MongoDB.Bson;

#endregion

namespace Netfox.Repository.Internals
{
    /// <summary>
    /// Base class of <see cref="StrongDocumentWrapper{TDocument}" />. This is used in situations where non-generic type is
    /// necessary. 
    /// It provides implementation of a document wrapper that keeps a strong reference to the document object.
    /// This wrapper is used for tracking documents in <see cref="DocumentState.Added"/>, <see cref="DocumentState.Deleted"/>,
    /// and <see cref="DocumentState.Modified"/> states.
    /// </summary>
    internal abstract class StrongDocumentWrapper : IDocumentWrapper
    {
        /// <summary>
        ///     Dictionary that caches <see cref="StrongDocumentWrapper{TDocument}" /> constructors for different TDocument types.
        /// </summary>
        private static readonly Dictionary<Type, Func<object, object>> FastCreate =
            new Dictionary<Type, Func<object, object>>();

        /// <summary>
        ///     Gets wrapper constructor activator function for the specified TDocument type.
        /// </summary>
        /// <param name="documentType">A type of the document for which the constructor activator is to be provided.</param>
        /// <returns>A wrapper constructor activator function for the specified TDocument type.</returns>
        internal static Func<object, object> WrapperActivator(Type documentType)
        {
            Func<object, object> dlg;
            if (FastCreate.TryGetValue(documentType, out dlg)) return dlg;
            var wrapperType = typeof (StrongDocumentWrapper<>).MakeGenericType(documentType);
            var value = Expression.Parameter(typeof (object), "document");

            var cinfo = wrapperType.GetConstructor(
                BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Instance, null,
                new[] {typeof (object)}, null);

            dlg = Expression.Lambda<Func<object, object>>(Expression.New(cinfo, value), value).Compile();
            FastCreate[documentType] = dlg;
            return dlg;
        }
        /// <summary>
        ///     Gets document object wrapped by the current instance.
        /// </summary>
        public abstract object Document { get; }
        /// <summary>
        ///     Gets <see cref="ObjectId" /> value of the document object wrapped by the current instance.
        /// </summary>
        public abstract ObjectId DocumentId { get; set; }
        /// <summary>
        ///     Gets the type of the document object wrapped by the current instance.
        /// </summary>
        public abstract Type DocumentType { get; }
        /// <summary>
        ///     Gets the collection name of the document object wrapped by the current instance.
        /// </summary>
        public abstract string CollectionName { get; set; }

        /// <summary>
        ///     Creates a <see cref="StrongDocumentWrapper" /> instance for the given document type and associates it with the
        ///     provided document.
        /// </summary>
        /// <param name="documentType">A type of the document.</param>
        /// <param name="document">Target document to be wrapped by the created <see cref="StrongDocumentWrapper" />.</param>
        /// <returns><see cref="StrongDocumentWrapper" /> instance for the given document type and provided document.</returns>
        internal static StrongDocumentWrapper Create([NotNull] Type documentType, [NotNull] object document)
        {
            if (documentType == null) throw new ArgumentNullException(nameof(documentType));
            if (document == null) throw new ArgumentNullException(nameof(document));
            var obj = WrapperActivator(documentType);
            return obj(document) as StrongDocumentWrapper;
        }
    }

    /// <summary>
    /// This class provides implementation of a document wrapper that keeps a strong reference to the document object.
    /// This wrapper is used for tracking documents in <see cref="DocumentState.Added"/>, <see cref="DocumentState.Deleted"/>,
    /// and <see cref="DocumentState.Modified"/> states.
    /// </summary>
    internal sealed class StrongDocumentWrapper<TDocument> : StrongDocumentWrapper where TDocument : class
    {
        /// <summary>
        /// Reference to the wrapped document.
        /// </summary>
        private readonly TDocument _document;

        /// <summary>
        ///     Creates a <see cref="StrongDocumentWrapper{TDocument}" /> instance that wraps the provided document.
        /// </summary>
        /// <param name="document">Target document to be wrapped by the created <see cref="StrongDocumentWrapper{TDocument}" />.</param>
        internal StrongDocumentWrapper([NotNull] TDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            _document = document;
        }

        /// <summary>
        ///     Creates a <see cref="StrongDocumentWrapper{TDocument}" /> instance that wraps the provided document.
        /// </summary>
        /// <param name="document">Target document to be wrapped by the created <see cref="StrongDocumentWrapper{TDocument}" />.</param>
        internal StrongDocumentWrapper([NotNull] object document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            _document = (TDocument) document;
        }

        /// <summary>
        ///     Gets document object wrapped by the current instance.
        /// </summary>
        public override object Document => _document;

        /// <summary>
        ///     Gets <see cref="ObjectId" /> value of the document object wrapped by the current instance. 
        ///     This value is read from the wrapped document.
        /// </summary>
        public override ObjectId DocumentId
        {
            get { return (Document as IDocument)?.Id ?? ObjectId.Empty; }
            set
            {
                var idoc = (Document as IDocument);
                if (idoc != null) idoc.Id = value;
            }
        }

        /// <summary>
        ///     Gets the collection name of the document object wrapped by the current instance.
        /// </summary>
        public override string CollectionName
        {
            get { return DocumentType.Name; }
            set { }
        }
        /// <summary>
        ///     Gets the type of the document object wrapped by the current instance.
        /// </summary>
        public override Type DocumentType => typeof (TDocument);
    }
}