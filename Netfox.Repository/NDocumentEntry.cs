//
//  NDocumentEntry.cs
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
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using FastMember;
using JetBrains.Annotations;
using MongoDB.Bson;
using Netfox.Repository.Internals;
using Netfox.Repository.Utils;

#endregion

namespace Netfox.Repository
{
    /// <summary>
    ///     An abstract class that represents untyped <see cref="NDocumentEntry" /> object. This object
    ///     is used by users to get metainformation about the associated document.
    /// </summary>
    public abstract class NDocumentEntry
    {
        /// <summary>
        ///     Keeps a reference to <see cref="NDocumentStateEntry" /> object which
        ///     provides internal state information about the document of the current entry.
        /// </summary>
        private readonly NDocumentStateEntry _documentStateEntry;

        /// <summary>
        /// Intializes a new instance of <see cref="NDocumentEntry"/>.
        /// </summary>
        /// <param name="context">The context in which this opject lives.</param>
        /// <param name="documentStateEntry">The state entry of the document.</param>
        /// <param name="document">The document object itself.</param>
        internal NDocumentEntry([NotNull] NRepositoryContext context, [NotNull] NDocumentStateEntry documentStateEntry,
            [NotNull] object document)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (documentStateEntry == null) throw new ArgumentNullException(nameof(documentStateEntry));
            if (document == null) throw new ArgumentNullException(nameof(document));

            Context = context;
            Id = documentStateEntry.Key;
            // Make strong reference to document so NDocumentStateEntry wont be reclaimed.        
            Document = document;
            _documentStateEntry = documentStateEntry;
        }

        /// <summary>
        ///     Provides a notification handler for the current entry.
        /// </summary>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        protected NotifyCollectionChangedEventHandler GetCollectionChangeHandler(string propertyName)
        {
            return
                (obj, args) =>
                    Context.DocumentStateManager.OnDocumentControlledPropertyChanged(Document,
                        new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        ///     Gets the context that manages document of this entry.
        /// </summary>
        public NRepositoryContext Context { get; }

        /// <summary>
        ///     Gets the document associated with this entry.
        /// </summary>
        public object Document { get; }

        /// <summary>
        ///     Gets document Id of this entry.
        /// </summary>
        public ObjectId Id { get; }

        /// <summary>
        ///     Gets the document type of this entry.
        /// </summary>
        public abstract Type DocumentType { get; }


        /// <summary>
        /// Save  chnages made to object of this <see cref="NDocumentSet"/>.
        /// </summary>
        /// <returns></returns>
        public abstract Task SaveChangesAsync();
        /// <summary>
        /// Creates a new <see cref="NDocumentEntry{TDocument}"/> instance according the provided arguments.
        /// </summary>
        /// <param name="repositoryContext"></param>
        /// <param name="documentStateEntry"></param>
        /// <param name="document"></param>
        /// <returns></returns>
        internal static NDocumentEntry Create(NRepositoryContext repositoryContext, NDocumentStateEntry documentStateEntry, object document)
        {
            var obj = WrapperActivator(documentStateEntry.DocumentType);
            return obj(repositoryContext, documentStateEntry, document) as NDocumentEntry;

        }
        /// <summary>
        ///     Dictionary that caches <see cref="NDocumentEntry{TDocument}" /> constructors for different TDocument types.
        /// </summary>
        private static readonly Dictionary<Type, Func<NRepositoryContext, NDocumentStateEntry, object, object>> FastCreate =
            new Dictionary<Type, Func<NRepositoryContext, NDocumentStateEntry, object, object>>();

        /// <summary>
        ///     Gets wrapper constructor activator function for the specified TDocument type.
        /// </summary>
        /// <param name="documentType">A type of the document for which the constructor activator is to be provided.</param>
        /// <returns>A wrapper constructor activator function for the specified TDocument type.</returns>
        internal static Func<NRepositoryContext, NDocumentStateEntry, object, object> WrapperActivator(Type documentType)
        {
            Func<NRepositoryContext, NDocumentStateEntry, object, object> dlg;
            if (FastCreate.TryGetValue(documentType, out dlg)) return dlg;
            var wrapperType = typeof(NDocumentEntry<>).MakeGenericType(documentType);
            var argContext = Expression.Parameter(typeof(NRepositoryContext), "context");
            var argStateEntry = Expression.Parameter(typeof(NDocumentStateEntry), "documentStateEntry");
            var argDocument = Expression.Parameter(typeof(object), "document");

            var cinfo = wrapperType.GetConstructor(
                BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Instance, null,
                new[] { typeof(object) }, null);

            dlg = Expression.Lambda<Func<NRepositoryContext, NDocumentStateEntry, object, object>>(Expression.New(cinfo, argContext, argStateEntry, argDocument), argDocument).Compile();
            FastCreate[documentType] = dlg;
            return dlg;
        }
    }

    /// <summary>
    ///     Instances of this class provide access to information about and control of
    ///     documents that are being tracked by the NRepositoryContext. Use the Document or Documents
    ///     methods of the context to obtain objects of this type.
    /// </summary>
    /// <typeparam name="TDocument">The type of document.</typeparam>
    /// <remarks>
    ///     This class follows flywight pattern. Thus it is immutable and can be create in many instances refering to the same
    ///     document.
    /// </remarks>
    public class NDocumentEntry<TDocument> : NDocumentEntry
        where TDocument : class, new()
    {
        internal static readonly TypeAccessor DocumentAccessor = TypeAccessor.Create(typeof (TDocument));

        internal static readonly Dictionary<string, PropertyInfo> NavigableReferencePropertiesInfo =
            CachedTypeInfo.GetNavigableReferenceProperties(typeof (TDocument)).ToDictionary(p => p.Name);

        internal static readonly Dictionary<string, PropertyInfo> NavigableCollectionPropertiesInfo =
            CachedTypeInfo.GetNavigableCollectionProperties(typeof (TDocument)).ToDictionary(p => p.Name);

        internal static readonly Dictionary<string, PropertyInfo> ScalarPropertiesInfo =
            CachedTypeInfo.GetScalarProperties(typeof (TDocument)).ToDictionary(p => p.Name);

        internal static readonly Dictionary<string, PropertyInfo> ComplexPropertiesInfo =
            CachedTypeInfo.GetComplexProperties(typeof (TDocument)).ToDictionary(p => p.Name);

        /// <summary>
        ///     Creates a proxy object for provided document within the specified repository context.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="documentStateEntry"></param>
        /// <param name="document"></param>
        internal NDocumentEntry(NRepositoryContext context, NDocumentStateEntry documentStateEntry, object document)
            : base(context, documentStateEntry, document)
        {
        }

        /// <summary>
        ///     Gets the document.
        /// </summary>
        public new TDocument Document => base.Document as TDocument;

        /// <summary>
        ///     Gets the type of the document associated with the current <see cref="NDocumentEntry" />.
        /// </summary>
        public override Type DocumentType => typeof (TDocument);

        /// <summary>
        ///     Gets the <see cref="NCollectionEntry{TDocument,TElement}" /> instance for the
        ///     specified collection property.
        /// </summary>
        /// <typeparam name="TElement">Type of items in the collection.</typeparam>
        /// <param name="propertyName">A name of the collection property.</param>
        /// <returns>
        ///     The <see cref="NCollectionEntry{TDocument,TElement}" /> instance for the
        ///     specified collection property; or <c>null</c> if the document
        ///     has not a collection property of this name.
        /// </returns>
        public NCollectionEntry<TDocument, TElement> Collection<TElement>(string propertyName)
            where TElement : class, new()
        {
            return NavigableCollectionPropertiesInfo.ContainsKey(propertyName)
                ? new NCollectionEntry<TDocument, TElement>(this, propertyName)
                : null;
        }

        /// <summary>
        ///     Gets an object that represents a scalar or complex property of this document.
        /// </summary>
        /// <typeparam name="TProperty"></typeparam>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public NPropertyEntry<TDocument, TProperty> Property<TProperty>(string propertyName)
            where TProperty : class
        {
            return ScalarPropertiesInfo.ContainsKey(propertyName) || ComplexPropertiesInfo.ContainsKey(propertyName)
                ? new NPropertyEntry<TDocument, TProperty>(this, propertyName)
                : null;
        }

        /// <summary>
        ///     Loads referenced object using the Repository Context and assigns it to the navigable reference property.
        /// </summary>
        /// <typeparam name="TProperty"></typeparam>
        /// <param name="referenceEntry">NReferenceEntry object describing the navigable reference.</param>
        internal async Task LoadReferenceAsync<TProperty>(NReferenceEntry<TDocument, TProperty> referenceEntry)
            where TProperty : class
        {
            var rd = Document as IDocument;
            if (rd == null) return;

            object val;
            if (rd.Navigable.TryGetValue(referenceEntry.Name, out val))
            {
                var oid = (ObjectId) val;
                var obj = await Context.FindObjectByIdAsync<TProperty>(oid);
                DocumentAccessor[Document, referenceEntry.Name] = obj;
            }
        }


        /// <summary>
        ///     Loads the navigable collection of objects using the Repository context and assigns it to the navigable collection
        ///     property.
        /// </summary>
        /// <typeparam name="TElement"></typeparam>
        /// <param name="collectionEntry">NcollectionEntry object that describes the collection to be loaded from the data store.</param>
        internal async Task LoadCollectionAsync<TElement>(NCollectionEntry<TDocument, TElement> collectionEntry)
            where TElement : class, new()
        {
            var rd = Document as IDocument;
            if (rd == null) return;
            object val;
            NHashSet<TElement> newSet;
            if (!rd.Navigable.TryGetValue(collectionEntry.Name, out val)) return;
            if (val != null)
            {
                var oids = (ObjectId[]) val;
                var docs = await Context.FindObjectsByIdsAsync<TElement>(oids);
                newSet = new NHashSet<TElement>(docs);
            }
            else
            {
                newSet = new NHashSet<TElement>();
            }
            newSet.CollectionChanged += GetCollectionChangeHandler(collectionEntry.Name);
            DocumentAccessor[Document, collectionEntry.Name] = newSet;
        }

        /// <summary>
        ///     Gets an object that represents the reference (for example, non-collection) navigation property from this document
        ///     to another document.
        /// </summary>
        /// <typeparam name="TProperty"></typeparam>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public NReferenceEntry<TDocument, TProperty> Reference<TProperty>(string propertyName)
            where TProperty : class
        {
            return NavigableReferencePropertiesInfo.ContainsKey(propertyName)
                ? new NReferenceEntry<TDocument, TProperty>(this, propertyName)
                : null;
        }

        /// <summary>
        ///     Reloads the entity from the database overwriting any property values with values from the database.
        ///     The document will be in the Unchanged state after calling this method.
        /// </summary>
        public async Task ReloadAsync()
        {
            await Context.ReloadDocumentAsync(this).ConfigureAwait(false);
        }

        /// <summary>
        ///     Saves changes of the document to the database collection.
        /// </summary>
        public override async Task SaveChangesAsync()
        {
            await Context.SaveChangesAsync(this).ConfigureAwait(false);
        }
    }
}