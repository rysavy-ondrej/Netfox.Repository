//
//  CacheableInterceptor.cs
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
	using System;
	using System.Runtime.Caching;
	using System.Collections.Generic;
	using System.Linq;
	using 
	using NRepository.Core.Query;

    /// <summary>
    /// This class implements an interceptor that provides a simple object caching. Caching is realized 
    /// by using MemoryCache object. It has some implicit settings that can be override in 
    /// application configuration file. It maintains strong references to cached objects. It is possilbe that some object
    /// is created more than once if it expires from the cache but is maintained elsewhere. 
    /// </summary>
	public class CacheableInterceptor : IQueryInterceptor
	{
		private static readonly object SyncObject = new object();
		private static readonly MemoryCache Cache = MemoryCache.Default;
	    private static readonly IEnumerable<Type> CacheableTypes = new Type[] {};

		public IQueryable<T> Query<T>(IQueryRepository repository, IQueryable<T> query, object additionalQueryData) where T : class
		{
			// It's not a cachable type
			if (!CacheableTypes.Contains(typeof(T)))
				return query;

			var key = typeof(T).AssemblyQualifiedName;
			var items = Cache.Get(key);

			// Is it already cached
			if (items != null)
				return (IQueryable<T>)items;

			lock(SyncObject)
			{
				items = Cache.Get(key);
				if (items == null)
				{
                    items = repository.GetEntities<T>().ToArray().AsQueryable();                    
					Cache.Add(key, items, new CacheItemPolicy());
				}

				return (IQueryable<T>)items;
			}
		}
	}
}

