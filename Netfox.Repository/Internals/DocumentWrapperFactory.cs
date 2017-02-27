using System;
using System.Net;
using JetBrains.Annotations;
using MongoDB.Bson;

namespace Netfox.Repository.Internals
{
    internal static class DocumentWrapperFactory
    {
        internal static IDocumentWrapper NullWrapper => NullDocumentWrapper.NullWrapper;

        /// <summary>
        /// Creates a new document wrapper depending on its initial state. 
        /// </summary>
        /// <remarks>
        /// If the state is <see cref="DocumentState.Unchanged"/> then 
        /// <see cref="WeakDocumentWrapper"/> is created. It the state is one of
        /// <see cref="DocumentState.Added"/>,<see cref="DocumentState.Deleted"/> or
        /// <see cref="DocumentState.Modified"/> then  <see cref="StrongDocumentWrapper"/> is created.
        /// </remarks> 
        /// <param name="entity"></param>
        /// <param name="key"></param>
        /// <param name="objectType"></param>
        /// <param name="state"></param>
        /// <returns></returns>
        internal static IDocumentWrapper CreateNewWrapper([CanBeNull] object entity, ObjectId key, [NotNull] Type objectType, DocumentState state)
        {
            return CreateNewWrapper(entity, key, objectType.Name, state);
        }

        /// <summary>
        /// Creates a new document wrapper depending on its initial state. 
        /// </summary>
        /// <remarks>
        /// If the state is <see cref="DocumentState.Unchanged"/> then 
        /// <see cref="WeakDocumentWrapper"/> is created. It the state is one of
        /// <see cref="DocumentState.Added"/>,<see cref="DocumentState.Deleted"/> or
        /// <see cref="DocumentState.Modified"/> then  <see cref="StrongDocumentWrapper"/> is created.
        /// </remarks> 
        /// <param name="entity"></param>
        /// <param name="key"></param>
        /// <param name="collection"></param>
        /// <param name="state"></param>
        /// <returns></returns>
        internal static IDocumentWrapper CreateNewWrapper([CanBeNull] object entity, ObjectId key, [NotNull] string collection, DocumentState state)
        {           
            if (entity == null)
            {
                return NullDocumentWrapper.NullWrapper;
            }

            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (collection == null) throw new ArgumentNullException(nameof(collection));
            if (key == ObjectId.Empty) throw new ArgumentOutOfRangeException(nameof(key));

            IDocumentWrapper wrapper = null;
            var documentType = entity.GetType();
            if (state == DocumentState.Unchanged || state == DocumentState.Detached)
            {
                wrapper = WeakDocumentWrapper.Create(documentType, entity);
            }
            else
            {
                wrapper = StrongDocumentWrapper.Create(documentType, entity);
            }
            wrapper.DocumentId = key;
            wrapper.CollectionName = collection;
            return wrapper;
        }
    }
}