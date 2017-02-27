using System;
using MongoDB.Bson;

namespace Netfox.Repository.Internals
{
    internal interface IDocumentWrapper
    {
        object Document { get; }

        ObjectId DocumentId { get; set; }

        Type DocumentType { get; }

        string CollectionName { get; set; }
    }
}