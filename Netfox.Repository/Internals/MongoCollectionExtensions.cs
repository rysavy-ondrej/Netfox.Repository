//
//  MongoCollectionExtensions.cs
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

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Netfox.Repository.Internals
{
    /// <summary>
    /// Provides extension methods for IMongoCollection object.
    /// </summary>
    static class MongoCollectionExtensions
    {
        /// <summary>
        /// Gets an object specified by its Id.
        /// </summary>
        /// <typeparam name="T">A type of the document to be retrieved.</typeparam>
        /// <param name="collection">An IMongoCollection object used to find the object.</param>
        /// <param name="id">An ObjectId specifying id of the object to get.</param>
        /// <returns>A document with the specified Id; null if the document with the specified Id cannot be found.</returns>
        /// <remarks>
        /// If you need to call this method synchronously, you can use the following snippet:
        /// <code>
        /// Task.Run(() => collection.FindOneByIdAsAsync()).Result;
        /// </code>
        /// </remarks>
        internal static async Task<T> FindOneByIdAsync<T>(this IMongoCollection<T> collection, ObjectId id)
        {
            var filter = Builders<T>.Filter.Eq("_id", id);
            var result = await collection.Find(filter).ToListAsync();
            return result.FirstOrDefault();
        }
        /// <summary>
        /// Gets a collection of objects specified by their ids.
        /// </summary>
        /// <typeparam name="T">A type of the document to be retrieved.</typeparam>
        /// <param name="collection">An IMongoCollection object used to find the object.</param>
        /// <param name="ids">An enumeration of ObjectId objects specifying ids of the objects to get.</param>
        /// <returns>An enumeration of documents with the specified Ids. This collection can be shorten that the collection of provided ids.</returns>
        internal static async Task<IEnumerable<T>> FindManyByIdsAsync<T>(this IMongoCollection<T> collection, IEnumerable<ObjectId> ids)
        {
            var filter = Builders<T>.Filter.In("_id", ids);
            var result = await collection.Find(filter).ToListAsync();
            return result;
        }
    }
}