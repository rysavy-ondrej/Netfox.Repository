using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Netfox.Repository.Internals;
using Netfox.Repository.Utils;
namespace Netfox.Repository.Tests.Utils
{
    class CachedObject
    {
        internal int Id;
        internal bool Dead;
        byte[] data;
        internal CachedObject(int id, int size)
        {
            this.Id = id;
            this.Dead = false;
            this.data = new byte[size];
        }        
    }
    [TestClass]
    public class NDocumentCacheTest
    {
        [TestMethod]
        public void TestTryGetValue()
        {
            // create cache
            var cache = new NDocumentCache<int, CachedObject>((k,v) => v.Dead);
            var objs = new CachedObject[10];
            // create 10 objects:
            for (int i = 0; i < 10; i++)
            {
                var co = new CachedObject(i, 10000000);
                objs[i] = co;
                cache.Set(i, co);    
            }

            CachedObject o;
            Assert.IsTrue(cache.TryGetValue(5, out o) && o == objs[5],"Item is in the cache but TryGetValue returns false.");
            objs[5].Dead = true;
            Assert.IsFalse(cache.TryGetValue(5, out o),"Item is in the cache but it is dead. TryGetValue returns true.");
                                                                                                                
            Assert.IsFalse(cache.TryGetValue(20, out o), "Item is not in the cache but TryGetValue returns true.");
        }

        [TestMethod]
        public void TestFlush()
        {
            // create cache
            var cache = new NDocumentCache<int, CachedObject>((k, v) => v.Dead);
            var objs = new CachedObject[10];
            // create 10 objects:
            for (int i = 0; i < 10; i++)
            {
                var co = new CachedObject(i, 10000000);
                objs[i] = co;
                cache.Set(i, co);
            }

        }        
    }
}
