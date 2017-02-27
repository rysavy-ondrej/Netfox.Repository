//
//  Program.cs
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Netfox.Repository;
using Netfox.Repository.Attributes;

namespace Netfox.Repository.Demo
{

    public class Empty
    {
        private static void Function<T>(T item) where T : new()
        {
        }

        private void Test()
        {
            var bc = new BadClass(1);          
        }

    }

    [NDocument]
    public class EmptyPoco
    { }

    public class BadClass
    {
        public BadClass(int x)
        {
        }
    }


    [NDocument]
    public class EmptyPoco1
    {
        [NValue]
        public int Member { get; set; }
    }


    [NDocument]
    public class Foo
    {
        [NValue]
        public string FooName { get; set; }
    }

    [NDocument]
    public class Bar
    {
        [NValue]
        public string BarName { get; set; }

        [NReference]
        public Bar Parent { get; set; }

        [NCollection(typeof(Foo))]
        public ICollection<Foo> Foos { get; set; }
    }

    class MainClass
    {
        public static void TestPoco()
        {
            var bar1 = new Bar() {BarName = "bar1"};
            var bar2 = new Bar() {BarName = "bar2"};
            bar1.Parent = bar2;
            for(var i = 0; i <10;i++)
            {
                var foo = new Foo() {FooName = $"Foo no.{i}"};
                bar1.Foos.Add(foo);
            }
            var bsonDoc = new BsonDocument();
            var writer = new BsonDocumentWriter(bsonDoc);
            BsonSerializer.Serialize(writer, bar1);

            var bar3 = BsonSerializer.Deserialize<Bar>(bsonDoc);
        }


        public static async  Task TestDatabase()
        {
            // drop the table...
            await NRepositoryContext.DeleteDatabaseAsync("Netfox");
            using (var repo = NRepositoryContext.Create("Netfox"))
            {

                var fooCol = repo.Collection<Foo>();
                var barCol = repo.Collection<Bar>();

                var bar1 = new Bar() {BarName = "bar1"};
                var bar2 = new Bar() {BarName = "bar2"};
                bar1.Parent = bar2;
                for (var i = 0; i < 10; i++)
                {
                    var foo = new Foo() {FooName = $"Foo no.{i}"};
                    bar1.Foos.Add(foo);
                    fooCol.Add(foo);
                }
                barCol.Add(bar1);
                barCol.Add(bar2);
                await repo.SaveChangesAsync();
                // Query database:
                var bar3 = (await barCol.FindAsync((bar1 as IDocument)?.Id ?? ObjectId.Empty));
                // check the identity:
                Debug.Assert(bar1 == bar3);
                if (bar3.Parent == null)
                {
                    var be = repo.Entry(bar3);
                    Debug.Assert(!be.Reference<Bar>(nameof(Bar.Parent)).IsLoaded);
                    be.Reference<Bar>(nameof(Bar.Parent)).Load();
                    // load again...
                    be.Reference<Bar>(nameof(Bar.Parent)).Load();
                    Debug.Assert(be.Reference<Bar>(nameof(Bar.Parent)).IsLoaded);
                    Debug.Assert(bar3.Parent != null);
                }
                if (bar3.Foos == null)
                {
                    var be = repo.Entry(bar3);
                    be.Collection<Foo>(nameof(Bar.Foos)).Load();
                    Debug.Assert(bar3.Foos != null);
                }
            }
        }

        
        public static async Task PerformanceTestInsert(NRepositoryContext repo)
        {
            var fooCol = repo.Collection<Foo>();
            var barCol = repo.Collection<Bar>();
            var stopwatch = Stopwatch.StartNew();
            NConsole.WriteLine($"This performance test inserts {_barCount} Bar objects each of which has {_fooCount} foo objects associated.");
            NConsole.WriteLine("Inserting data to the database...");
            int count = 0;
            var barcount = _barCount;
            for (var i = 0; i < barcount; i++)
            {
                var bar = new Bar()
                {
                    BarName = $"Bar No.{i}"
                };
                for (var j = 0; j < _fooCount; j++)
                {
                    var foo = new Foo()
                    {
                        FooName = $"Foo No.{j}, which belongs to Bar no.{i}"
                    };
                    bar.Foos.Add(foo);                  
                }
                fooCol.AddRange(bar.Foos);
                barCol.Add(bar);
                ++count;                               
                await repo.SaveChangesAsync();
                NConsole.Write($"\rSaved objects: {count:N} of {barcount:N}, insert speed {GetDps(count,stopwatch):N} dps.  ");
            }
            stopwatch.Stop();
            NConsole.WriteLine($"\n\rInsert completed ({stopwatch.ElapsedMilliseconds/1000.0:N} ms), insert speed: {GetDps(count, stopwatch):N} dps.  ");
        }


        static async Task PerformanceTestUpdate(NRepositoryContext ctx)
        {
            NConsole.WriteLine("UPDATE AND SAVE TEST");
            NConsole.WriteLine("This test loads all Foo objects that contains 'belongs' in its FooName and " +
                             "replace this word by 'is owned by'. The objects are then updated in the DB.");
            var stopwatch = Stopwatch.StartNew();
            var foocol = ctx.Collection<Foo>();
            var fooset = (await foocol.FindAsync(f => f.FooName.Contains("belongs"))).ToArray();
            stopwatch.Stop();
            NConsole.WriteLine($"\nObjects loaded from the database ({stopwatch.ElapsedMilliseconds:N} ms), updating in memory...");
            stopwatch.Restart();
            int count = 0;
            foreach (var foo in fooset)
            {
                foo.FooName = foo.FooName.Replace("belongs", "is owned by");
                if (++count % 1000 == 0) NConsole.Write($"\r Updated {count:N} / {fooset.Count():N}    ");
            }
            stopwatch.Restart();
            NConsole.WriteLine($"\nObjects updated in memory ({stopwatch.ElapsedMilliseconds:N} ms), saving changes to the database...");
            stopwatch.Restart();
            await ctx.SaveChangesAsync();
            stopwatch.Stop();
            NConsole.WriteLine($"\nObjects updated in database ({stopwatch.ElapsedMilliseconds:N} ms), test done.");
        }

        static async Task PerformanceTestUpdateAndReload(NRepositoryContext ctx)
        {
            NConsole.WriteLine("UPDATE AND RELOAD TEST");
            NConsole.WriteLine("This test loads all Foo objects that contains 'belongs' in its FooName and " +
                             "replace this word by 'is owned by'. The objects are then reloaded from the DB.");
            var stopwatch = Stopwatch.StartNew();
            var foocol = ctx.Collection<Foo>();
            var fooset = (await foocol.FindAsync(f => f.FooName.Contains("belongs"))).Take(1000).ToArray();
            stopwatch.Stop();
            NConsole.WriteLine($"\nObjects {fooset.Count():N} loaded from the database ({stopwatch.ElapsedMilliseconds:N} ms), updating in memory...");
            stopwatch.Restart();
            int count = 0;
            foreach (var foo in fooset)
            {
                foo.FooName = foo.FooName.Replace("belongs", "is owned by");
                if (++count%1000 == 0) NConsole.Write($"\r Updated {count:N} / {fooset.Count():N}    ");
            }
            stopwatch.Restart();
            NConsole.WriteLine($"\nObjects updated in memory ({stopwatch.ElapsedMilliseconds:N} ms), reloading them from the database...");
            stopwatch.Restart();
            foreach (var foo in fooset)
            {
                await ctx.Entry(foo).ReloadAsync();            
            }
            stopwatch.Stop();
            NConsole.WriteLine($"\nObjects reloaded from the database ({stopwatch.ElapsedMilliseconds:N} ms), test done.");
        }

        public static async Task PerformanceTestReadAndCollectionResolve(NRepositoryContext repo)
        {
            var fooCol = repo.Collection<Foo>();
            var barCol = repo.Collection<Bar>();
            var barcount = await barCol.CountAsync();
            var sw = Stopwatch.StartNew();
            NConsole.WriteLine("READ TEST: LOAD AND COLLECTION RESOLVE");
            NConsole.WriteLine($"This performance test loads {barcount} Bar objects and resolves Foos collection for each Bar object.");
            NConsole.WriteLine("Loading data from the database...");
            int count = 0;
            foreach (var b in (IEnumerable<Bar>)barCol)
            {
                repo.Entry(b).Collection<Foo>(nameof(Bar.Foos)).Load();
                NConsole.Write($"\r{b.BarName}, {++count:N} of {barcount:N}, load speed {GetDps(count,sw):N} dps.");
            }
            sw.Stop();
            NConsole.WriteLine($"\rLoad complete ({sw.ElapsedMilliseconds:N} ms), read speed is {GetDps(count, sw):N} dps");
        }

        static double GetDps(int count, Stopwatch sw)
        {
            var ms = Math.Max(sw.ElapsedMilliseconds, 1);
            return count*1000.0/ms;
        }

        public static async Task PerformanceTestReadSimple(NRepositoryContext repo)
        {
            var fooCol = repo.Collection<Foo>();
            var foocount = (int)(await fooCol.CountAsync());
            NConsole.WriteLine("READ TEST: LOAD COLLECTION");
            NConsole.WriteLine($"This performance test loads all Foo objects ({foocount:N} items)  from the database.");
            NConsole.WriteLine("Loading data from the database...");
            var sw = Stopwatch.StartNew();
            foreach (var b in fooCol) { }
            sw.Stop();
            NConsole.WriteLine($"\rRead completed ({sw.ElapsedMilliseconds} ms), read speed is {GetDps(foocount, sw):N} dps.");
        }

        public static async Task PerformanceTestFindByStringProperty(NRepositoryContext repo)
        {
            NConsole.WriteLine("FIND TEST: FIND BY STRING VALUE USING FLUENT API");
            NConsole.WriteLine("This performance test search for all Foo objects whose FooName value ends with '0'.\n");
            
            var fooCol = repo.Collection<Foo>();
            var fb = new FilterDefinitionBuilder<Foo>();
            var sw = Stopwatch.StartNew();

            // { FooName: { $regex: / 10$/, $options: 'm'} }");
            var ft = fb.Regex(f=> f.FooName, new BsonRegularExpression("0$", "m"));
            var result = await fooCol.MongoCollection.Find(ft).ToListAsync();
            sw.Stop();
            NConsole.WriteLine($"Total results {result.Count} ({sw.ElapsedMilliseconds} ms), read speed is {GetDps(result.Count, sw):N} dps.");
        }

        public static async Task PerformanceTestFluentFindUsingFluent(NRepositoryContext repo)
        {
            NConsole.WriteLine("FIND TEST: FIND BY STRING VALUE USING EXPRESSION TREE");
            NConsole.WriteLine("This performance test search for all Foo objects whose FooName value ends with '0'. It uses FindFluent Api to construct a query.");
            var sw = Stopwatch.StartNew();

            var fooCol = repo.Collection<Foo>();

            var result = await fooCol.FindAsync(f => f.FooName.EndsWith("0"));
            var count = result.Count();
            sw.Stop();
            NConsole.WriteLine($"Total results {count} ({sw.ElapsedMilliseconds} ms), read speed is {GetDps(count, sw):N} dps.");

        }

        public static void PerformanceTestFindByStringPropertyAsObservable(NRepositoryContext repo)
        {
            NConsole.WriteLine("FIND TEST: FIND BY STRING VALUE USING EXPRESSION TREE RETURN AS OBSERVABLE");
            NConsole.WriteLine("This performance test search for all Foo objects whose FooName value " +
                             "ends with '0'. It uses FindFluent Api to construct a query. Result is provided through IObservable.");
            var sw = Stopwatch.StartNew();

            var observable = repo.ObservableCollection<Foo>();

            var count = 0;
            // using causes that when subscriber is disposed then it is automatically unsubscribed from the observable collection
            using (observable.Subscribe(x => { count++; }, () =>
            {
                NConsole.WriteLine("End of source stream reached!");
                sw.Stop();
            }))
            {
                var source = new CancellationTokenSource();
                var t = observable.FindAsync(f => f.FooName.EndsWith("0"), source.Token);
                NConsole.WriteLine("Press any key to unsubscribe...");
                Console.ReadKey();
                source.Cancel();
                t.Wait(source.Token);
            }

            NConsole.WriteLine($"Total results {count} ({sw.ElapsedMilliseconds} ms), read speed is {GetDps(count, sw):N} dps.");
        }
       
        static void ManyEmptyObjectsTest()
        {
            NConsole.WriteLine($"\nCreating {_testSize} instances of Empty class.");
            var fooCollection = new Empty[_testSize];
            long memstart = GC.GetTotalMemory(true);
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < _testSize; i++)
            {
                var foo = new Empty();
                fooCollection[i] = foo;
            }
            sw.Stop();
            long memstop = GC.GetTotalMemory(true);
            long memobj = (memstop - memstart) / _testSize;
            NConsole.WriteLine($"Created {_testSize} objects ({sw.ElapsedMilliseconds} ms). Allocation {memstop - memstart}B. Single object {memobj}B.");
        }

        static void ManyPocoObjectsTest()
        {
            NConsole.WriteLine($"\nCreating {_testSize} instances of EmptyPoco class.");
            var fooCollection = new EmptyPoco[_testSize];
            long memstart = GC.GetTotalMemory(true);
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < _testSize; i++)
            {
                var foo = new EmptyPoco();
                fooCollection[i] = foo;
            }
            sw.Stop();
            long memstop = GC.GetTotalMemory(true);
            long memobj = (memstop - memstart)/_testSize;
            NConsole.WriteLine($"Created {_testSize} objects ({sw.ElapsedMilliseconds} ms). Allocation {memstop-memstart}B. Single object {memobj}B.");
        }

        static void ManyPoco1ObjectsTest()
        {
            NConsole.WriteLine($"\nCreating {_testSize} instances of EmptyPoco1 class.");
            var fooCollection = new EmptyPoco1[_testSize];
            var sw = Stopwatch.StartNew();
            long memstart = GC.GetTotalMemory(true);            
            for (int i = 0; i < _testSize; i++)
            {
                var foo = new EmptyPoco1();
                fooCollection[i] = foo;
            }
            sw.Stop();
            long memstop = GC.GetTotalMemory(true);
            long memobj = (memstop - memstart) / _testSize;
            NConsole.WriteLine($"Created {_testSize} objects ({sw.ElapsedMilliseconds} ms). Allocation {memstop - memstart}B. Single object {memobj}B.");
        }

        static async Task CacheTestWrite(NRepositoryContext repo)
        {
            NConsole.WriteLine($"\nCreating {_testSize} instances of Foo class.");
            var fooCollection = repo.Collection<Foo>();
            var sw = Stopwatch.StartNew();
            long memstart = GC.GetTotalMemory(true);
            for (int i = 0; i < _testSize; i++)
            {
                var foo = new Foo {FooName = $"Foo_{i}"};
                fooCollection.Add(foo);
            }
            long memstop = GC.GetTotalMemory(true);
            long memobj = (memstop - memstart) / _testSize;
            sw.Stop();
            NConsole.WriteLine($"Added {_testSize} documents ({sw.ElapsedMilliseconds} ms). Allocation {memstop - memstart}B. Single object {memobj}B.");
            sw = Stopwatch.StartNew();
            await repo.SaveChangesAsync();
            sw.Stop();
            NConsole.WriteLine($"Changes saved ({sw.ElapsedMilliseconds} ms).");
        }

        static async Task CacheTestRead(NRepositoryContext repo)
        {
            var random = new Random();
            var fooCollectionUntyped = repo.Collection(typeof(Foo));
            var fooCollection = fooCollectionUntyped as NDocumentSet<Foo>;
            var colsize = await fooCollection.CountAsync();
            var count = 0;
            NConsole.WriteLine($"\nRandomly loading objects from the Foo collection ({colsize} items) using filter 'f => f.FooName.EndsWith'.");
            NConsole.WriteLine("Note that this is expesive query. No index is defined and regex search is utilized at server side.");
            NConsole.WriteLine("Press any key to abort the test and continue...");
            var sw = Stopwatch.StartNew();
            while (true)
            {
                count++;
                if (Console.KeyAvailable)
                {
                    Console.ReadKey();
                    break;
                }
                var r = random.Next(0, _testSize-1);
                var g = await fooCollection.FindOneAsync(f => f.FooName.EndsWith($"{r}"));
                if (g == null) continue;
                var qps = count * 1000.0/ sw.ElapsedMilliseconds;
                NConsole.Write($"\r#{count} {g.FooName}, perf = {qps:N} qps.    ");
            }
            sw.Stop();
            NConsole.WriteLine("\nDone. Perform GC.Collection to remove all cache entries...");
            GC.Collect();
        }
        private static int _testSize = 1000000;
        private static int _barCount = 1000;
        private static int _fooCount = 1000;

        public static void Main(string[] args)
        {
            System.Diagnostics.Trace.WriteLine($"{DateTime.Now}: Starting Netfox.Repository.Demo", "Information");
            NConsole.Init(120,60);
            NConsole.Clear();
            NConsole.SetCursorPosition(0,20);

            NConsole.WriteLine("Object initalization tests");
            ManyEmptyObjectsTest();
            NConsole.WriteHline();

            ManyPocoObjectsTest();
            NConsole.WriteHline();
            
            ManyPoco1ObjectsTest();
            NConsole.WriteHline();

            Thread.Sleep(1000);
            
            NConsole.Clear();
            NConsole.SetCursorPosition(0, 20);
            NConsole.WriteLine("Database operation tests");
            NConsole.Write("Preparing database context and registering types...");
            NRepositoryContext.DeleteDatabaseAsync("Netfox").Wait();
            NRepositoryContext.RegisterDocumentTypes(Assembly.GetExecutingAssembly());
            Thread.Sleep(100);
            NConsole.WriteLine("Ok!");
            Thread.Sleep(100);
            var ctx = NRepositoryContext.Create("Netfox");
            {
                var statTimer = new Timer(new TimerCallback(StatTimerCallback), ctx, 1000, 500);
                CacheTestWrite(ctx).Wait();
                NConsole.WriteHline();
                CacheTestRead(ctx).Wait();
                NConsole.WriteHline();
                Thread.Sleep(1000);                
                PerformanceTestInsert(ctx).Wait();
                NConsole.WriteHline();
                PerformanceTestReadAndCollectionResolve(ctx).Wait();
                NConsole.WriteHline();
                PerformanceTestReadSimple(ctx).Wait();
                NConsole.WriteHline();
                PerformanceTestUpdateAndReload(ctx).Wait();
                NConsole.WriteHline();
                PerformanceTestUpdate(ctx).Wait();
                NConsole.WriteHline();
                PerformanceTestFindByStringProperty(ctx).Wait();
                NConsole.WriteHline();
                PerformanceTestFluentFindUsingFluent(ctx).Wait();
                NConsole.WriteHline();
                PerformanceTestFindByStringPropertyAsObservable(ctx);
                NConsole.WriteHline();   

                NConsole.WriteLine("\n\nDone. Press any key to exit...");
                Console.ReadKey();
                System.Diagnostics.Trace.WriteLine($"{DateTime.Now}: Netfox.Repository.Demo completed.", "Information");
            }
            ctx.Dispose();
        }
        private static void StatTimerCallback(object state)
        {
            NConsole.EnterLock();
            try
            {
                long memstop = GC.GetTotalMemory(false);
                NConsole.PushPosition();
                NConsole.Rectangle(0, 0, 80, 20);
                NConsole.SetCursorPosition(0, 2);
                (state as NRepositoryContext)?.GetStatistics().Print(Console.Out);                
                Console.WriteLine($" GC Memory Allocation: {memstop:N} Bytes.");
                NConsole.PopPosition();
            }
            finally
            {
                NConsole.ExitLock();
            }
        }
    }
}

