using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using FastMember;
using JetBrains.Annotations;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Netfox.Repository.Serialization
{
    /// <summary>
    ///     This callback is used to inform an observer that a new document is
    ///     to be deserialized. Using this callback it is possible to skip deserialization
    ///     by providing cached object as the result of this callback.
    /// </summary>
    /// <param name="oid"></param>
    /// <param name="documentType"></param>
    /// <returns>
    ///     An object from the cache to be used instead of deserialized object, or null
    ///     if object should be fully deserialized.
    /// </returns>
    public delegate object BeforeDocumentDeserializationCallback(ObjectId oid, Type documentType);

    /// <summary>
    ///     This callback is used to inform an observer that a new document has been desrialized.
    ///     It can be used to add this object to the cache.
    /// </summary>
    /// <param name="oid"></param>
    /// <param name="documentType"></param>
    /// <param name="document"></param>
    public delegate void AfterDocumentDeserializationCallback(ObjectId oid, Type documentType, object document);

    /// <summary>
    /// An abstract base class of serializers for all classes that implements <see cref="IDocument"/> interface.
    /// </summary>
    public abstract class NDocumentSerializer : IBsonSerializer
    {
        /// <summary>
        /// Provides <see cref="ObjectId"/> array serializer instance.
        /// </summary>
        public static readonly ArraySerializer<ObjectId> ObjectIdArraySerializer = new ArraySerializer<ObjectId>();
        /// <summary>
        /// Represents a callback function called after the document has been deserialized.
        /// </summary>
        /// <remarks>
        /// <see cref="NRepositoryContext"/> uses this callback to intercept deserialization process.
        /// </remarks>
        public AfterDocumentDeserializationCallback AfterDocumentDeserialization;
        /// <summary>
        /// Represents a callback function called before the document is to deserialized.
        /// </summary>
        /// <remarks>
        /// <see cref="NRepositoryContext"/> uses this callback to intercept deserialization process.
        /// </remarks>
        public BeforeDocumentDeserializationCallback BeforeDocumentDeserialization;
        /// <summary>
        /// Deserializes an object from a <see cref="BsonDeserializationContext"/>. 
        /// </summary>
        /// <param name="context">Deserialization context containing BsonReader that can be used to read object from.</param>
        /// <param name="args">Additional deserialization arguments.</param>
        /// <returns>An object deserialized from the <paramref name="context"/>.</returns>
        public abstract object Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args);
        /// <summary>
        /// Serializes an object to a <see cref="BsonSerializationContext"/>. 
        /// </summary>
        /// <param name="context">Serialization context containing BsonWriter to write the object to.</param>
        /// <param name="args">Additional serializatio arguments.</param>
        /// <param name="value">An object to be serialized.</param>
        public abstract void Serialize(BsonSerializationContext context, BsonSerializationArgs args, object value);
        /// <summary>
        /// Represents a type of object that can be serialized and deserialized by this serializer.
        /// </summary>
        public abstract Type ValueType { get; }
    }

    /// <summary>
    ///     Implements a custom serializer for persistent documents.
    /// </summary>
    /// <typeparam name="TDocument">A document type supported by the serializer.</typeparam>
    public class NDocumentSerializer<TDocument> : NDocumentSerializer, IBsonSerializer<TDocument>,
        IBsonDocumentSerializer
        where TDocument : class, new()
    {
        /// <summary>
        /// Tries to get the serialization info for a member. 
        /// </summary>
        /// <param name="memberName">Name of the member.</param>
        /// <param name="serializationInfo">The serialization information.</param>
        /// <returns>true if the serialization info exists; otherwise false. </returns>
        public bool TryGetMemberSerializationInfo(string memberName, out BsonSerializationInfo serializationInfo)
        {
            PropertyInfo property;
            if (NDocumentEntry<TDocument>.ScalarPropertiesInfo.TryGetValue(memberName, out property))
            {
                var typ = property.PropertyType;
                serializationInfo = new BsonSerializationInfo(memberName, BsonSerializer.LookupSerializer(typ), typ);
                return true;
            }
            if (NDocumentEntry<TDocument>.ComplexPropertiesInfo.TryGetValue(memberName, out property))
            {
                var typ = property.PropertyType;
                serializationInfo = new BsonSerializationInfo(memberName, BsonSerializer.LookupSerializer(typ), typ);
                return true;
            }
            serializationInfo = null;
            return false;
        }
        /// <summary>
        /// 
        /// </summary>
        public override Type ValueType => typeof (TDocument);

        /// <summary>
        /// This is a special deserializer that is able to load class from even partially 
        /// serialized BsonDocument.
        /// </summary>
        /// <param name="context">The deserialization context.</param>
        /// <param name="args">The deserialization args.</param>
        /// <returns>A deserialized value</returns>
        public override object Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            context.Reader.ReadStartDocument();
            var oid = context.Reader.ReadObjectId();
            var document = BeforeDocumentDeserialization?.Invoke(oid, typeof (TDocument)) as TDocument;
            // if no document is provided, just skip the deserialization...
            if (document == null)
            {
                while (context.Reader.State != BsonReaderState.EndOfDocument)
                {
                    switch (context.Reader.State)
                    {
                        case BsonReaderState.Name:
                            context.Reader.SkipName();
                            break;
                        case BsonReaderState.Value:
                            context.Reader.SkipValue();
                            break;
                        case BsonReaderState.Type:
                            context.Reader.ReadBsonType();
                            break;
                        default:
                            throw new InvalidOperationException("Unexpected element found in the stream.");
                    }
                }
                context.Reader.ReadEndDocument();
            }
            else
            {
                var idocument = document as IDocument;
                if (idocument == null) return document;

                Debug.Assert(idocument.Id.Equals(oid));
                        
                var lastName = string.Empty;
                BsonType lastType = default(BsonType);
                while (context.Reader.State != BsonReaderState.EndOfDocument)
                {
                    switch (context.Reader.State)
                    {
                        case BsonReaderState.Name:
                            lastName = context.Reader.ReadName();
                            break;
                        case BsonReaderState.Value:
                            ReadValue(idocument,context, lastName);
                            break;
                        case BsonReaderState.Type:
                            lastType = context.Reader.ReadBsonType();
                            break;
                        default:
                            throw new InvalidOperationException("Unexpected element found in the stream.");
                    }
                }
                context.Reader.ReadEndDocument();
                AfterDocumentDeserialization?.Invoke(oid, typeof (TDocument), document);
            }
            return document;
        }

        private void ReadValue(IDocument idocument, BsonDeserializationContext context, string propertyName)
        {
            PropertyInfo propertyInfo;
            if (NDocumentEntry<TDocument>.ScalarPropertiesInfo.TryGetValue(propertyName, out propertyInfo) ||
                NDocumentEntry<TDocument>.ComplexPropertiesInfo.TryGetValue(propertyName, out propertyInfo))
            {
                idocument.SetPropertyValue(propertyName,BsonSerializer.Deserialize(context.Reader, propertyInfo.PropertyType));
                return;
            }
            if (NDocumentEntry<TDocument>.NavigableReferencePropertiesInfo.TryGetValue(propertyName, out propertyInfo))
            {
                idocument.Navigable[propertyName] = BsonSerializer.Deserialize(context.Reader, typeof(ObjectId));
                idocument.SetPropertyValue(propertyName, null);
                return;
            }
            if (NDocumentEntry<TDocument>.NavigableCollectionPropertiesInfo.TryGetValue(propertyName, out propertyInfo))
            {
                idocument.Navigable[propertyName] = ObjectIdArraySerializer.Deserialize(context);
                idocument.SetPropertyValue(propertyName, null);
                return;
            }
        }
        /// <summary>
        /// Serializes an object to a <see cref="BsonSerializationContext"/>. 
        /// </summary>
        /// <param name="context">The serialization context.</param>
        /// <param name="args">The serialization args.</param>
        /// <param name="value">The object to serialize.</param>
        public void Serialize(BsonSerializationContext context, BsonSerializationArgs args, TDocument value)
        {
            var doc = value as IDocument;
            var accessor = NDocumentEntry<TDocument>.DocumentAccessor;
            if (doc == null)
                throw new ArgumentException(
                    $"Provided object of type {typeof (TDocument)} is invalid. Check if this class is annotated with NDocument attribute.",
                    nameof(value));

            context.Writer.WriteStartDocument();
            context.Writer.WriteName("_id");
            context.Writer.WriteObjectId(doc.Id);


            foreach (var sp in NDocumentEntry<TDocument>.ScalarPropertiesInfo)
            {
                context.Writer.WriteName(sp.Key);
                BsonSerializer.Serialize(context.Writer, accessor[doc, sp.Key]);
            }
            foreach (var sp in NDocumentEntry<TDocument>.ComplexPropertiesInfo)
            {
                context.Writer.WriteName(sp.Key);
                BsonSerializer.Serialize(context.Writer, accessor[doc, sp.Key]);
            }
            foreach (var sp in NDocumentEntry<TDocument>.NavigableReferencePropertiesInfo)
            {
                var refObj = accessor[doc, sp.Key] as IDocument;
                context.Writer.WriteName(sp.Key);
                BsonSerializer.Serialize(context.Writer, refObj?.Id ?? ObjectId.Empty);
            }
            foreach (var sp in NDocumentEntry<TDocument>.NavigableCollectionPropertiesInfo)
            {
                var refObj = accessor[doc, sp.Key] as IEnumerable<IDocument>;
                context.Writer.WriteName(sp.Key);
                var array = refObj?.Select(obj => obj.Id).ToArray() ?? new ObjectId[] {};
                ObjectIdArraySerializer.Serialize(context, args, array);
            }
            context.Writer.WriteEndDocument();
        }

        TDocument IBsonSerializer<TDocument>.Deserialize(BsonDeserializationContext context,
            BsonDeserializationArgs args)
        {
            return Deserialize(context, args) as TDocument;
        }
        /// <summary>
        /// Serializes an object to a <see cref="BsonSerializationContext"/>. 
        /// </summary>
        /// <param name="context">The serialization context.</param>
        /// <param name="args">The serialization args.</param>
        /// <param name="value">The object to serialize.</param>
        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, object value)
        {
            var doc = value as TDocument;
            if (doc != null)
            {
                Serialize(context, args, doc);
            }
        }
    }
}