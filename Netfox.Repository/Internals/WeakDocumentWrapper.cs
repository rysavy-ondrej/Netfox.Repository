//
//  WeakDocumentWrapper.cs
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
//

#region

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using MongoDB.Bson;

#endregion

namespace Netfox.Repository.Internals
{
    /// <summary>
    ///     Base class of <see cref="WeakDocumentWrapper{TDocument}" />. This is used in situations where non-generic type is
    ///     necessary.
    ///     It provides implementation of Document wrapper that keeps only weak reference to the document object.
    ///     This wrapper is used for tracking documents in <see cref="DocumentState.Unchanged" /> state.
    /// </summary>
    internal abstract class WeakDocumentWrapper : IDocumentWrapper
    {
        /// <summary>
        ///     Dictionary that caches <see cref="WeakDocumentWrapper{TDocument}" /> constructors for different TDocument types.
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
            var wrapperType = typeof (WeakDocumentWrapper<>).MakeGenericType(documentType);
            var value = Expression.Parameter(typeof (object), "document");

            var cinfo = wrapperType.GetConstructor(
                BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Instance, null,
                new[] {typeof (object)}, null);
            //var cinfos = wrapperType.GetConstructors(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Instance);
            //var cinfo = cinfos.FirstOrDefault(x => x.GetParameters().Any(p => p.ParameterType == typeof (object)));

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
        ///     Creates a <see cref="WeakDocumentWrapper" /> instance for the given document type and associates it with the
        ///     provided document.
        /// </summary>
        /// <param name="documentType">A type of the document.</param>
        /// <param name="document">Target document to be wrapped by the created <see cref="WeakDocumentWrapper" />.</param>
        /// <returns><see cref="WeakDocumentWrapper" /> instance for the given document type and provided document.</returns>
        internal static WeakDocumentWrapper Create(Type documentType, object document)
        {
            var obj = WrapperActivator(documentType);
            return obj(document) as WeakDocumentWrapper;
        }
    }

    /// <summary>
    ///     This class implements a weak document wrapper. It means that a document is referenced using WeakReference
    ///     and thus it can be removed by GC if no other references exist.
    /// </summary>
    internal sealed class WeakDocumentWrapper<TDocument> : WeakDocumentWrapper where TDocument : class
    {
        /// <summary>
        ///     Keeps <see cref="WeakReference{TDocument}" /> that is used to reference to the wrapped document while
        ///     enabling GC to collect the document if it is not longer in use.
        /// </summary>
        private readonly WeakReference<TDocument> _document;

        /// <summary>
        ///     Creates a <see cref="WeakDocumentWrapper" /> instance that wraps the provided document.
        /// </summary>
        /// <param name="document">Target document to be wrapped by the created <see cref="WeakDocumentWrapper{TDocument}" />.</param>
        internal WeakDocumentWrapper(TDocument document)
        {
            _document = new WeakReference<TDocument>(document);
        }

        /// <summary>
        ///     Creates a <see cref="WeakDocumentWrapper" /> instance that wraps the provided document.
        /// </summary>
        /// <param name="document">Target document to be wrapped by the created <see cref="WeakDocumentWrapper{TDocument}" />.</param>
        internal WeakDocumentWrapper(object document)
        {
            _document = new WeakReference<TDocument>((TDocument) document);
        }

        /// <summary>
        ///     Gets document object wrapped by the current instance. It returns <code>null</code> if the wrapped document was reclaimed by GC.
        /// </summary>
        public override object Document
        {
            get
            {
                TDocument value = null;
                _document.TryGetTarget(out value);
                return value;
            }
        }

        /// <summary>
        ///     Gets <see cref="ObjectId" /> value of the document object wrapped by the current instance. This value is still valid even if the 
        ///     wrapped document was reclaimed by the GC.
        /// </summary>
        public override ObjectId DocumentId { get; set; }


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