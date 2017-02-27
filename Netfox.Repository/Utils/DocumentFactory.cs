using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FastMember;
using MongoDB.Bson;

namespace Netfox.Repository.Utils
{
    /// <summary>
    /// This class implements Document Factory pattern. 
    /// </summary>
    public static class DocumentFactory
    {
        /// <summary>
        /// Creates a new document of the specified type. This type must implement <see cref="IDocument"/>
        /// interface. 
        /// </summary>
        /// <param name="documentType">A type of document to create.</param>
        /// <param name="oid">An object id of the newly created document.</param>
        /// <returns>A new document of the specified type.</returns>
        /// <exception cref="InvalidCastException">if <paramref name="documentType"/> does not implement <see cref="IDocument"/></exception>
        public static object CreateNew(Type documentType, ObjectId oid)
        {
            var document = TypeAccessor.Create(documentType).CreateNew();
            ((IDocument)document).Id = oid;
            return document;
        }
    }
}
