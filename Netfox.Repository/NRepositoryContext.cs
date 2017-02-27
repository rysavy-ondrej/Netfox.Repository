//
//  NRepositoryContext.cs
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
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using JetBrains.Annotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Netfox.Repository.Internals;
using Netfox.Repository.Serialization;
using Netfox.Repository.Utils;

#endregion

namespace Netfox.Repository
{
    /// <summary>
    ///     This class provides access to underlying data repository represented by Mongo database and maintains document
    ///     states.
    /// </summary>
    /// <remarks>
    ///     Except the access to the MongoDB data store, this class performs document state management.
    ///     An application usually contains a single instace of the <see cref="NRepositoryContext" /> to consistently manage
    ///     the data documents.
    ///     <see cref="NRepositoryContext" /> is created by specifying a connection string to the databse server and the name
    ///     of the database at that server. Each context can be associated only with a sibngle database.
    ///     Documents in No-SQL data model live in collections. Use this class to access collections in the database associated
    ///     with the current <see cref="NRepositoryContext" />. A collection is represented by
    ///     <see cref="NDocumentSet{TDocument}" />
    ///     class that provides method for accessing individual documents.
    /// </remarks>
    /// <example>
    ///     <see cref="NRepositoryContext" /> can be used to write data to the data store as follows:
    ///     <code>
    /// using (var ctx = NRepositoryContext.Create("DataBase"))
    /// {
    ///     var usersCollection = ctx.Collection&lt;Users&gt;();
    ///     var newUser = new User {Name = "John Doe"};
    ///     usersCollection.Add(newUser);
    ///     await ctx.SaveChangesAsync();
    /// }
    /// </code>
    /// </example>
    /// <example>
    ///     <see cref="NRepositoryContext" /> can be used to read data from the data store as follows:
    ///     <code>
    /// using (var ctx = NRepositoryContext.Create("DataBase"))
    /// {
    ///     var usersCollection = ctx.Collection&lt;Users&gt;();
    /// 
    ///     var john = await fooCollection.FindOneAsync(user => user.Name.Contains("John"));
    ///     
    ///     if (john != null)
    ///     {
    ///         // do something with a retrieved document
    ///     }
    /// }
    /// </code>
    /// </example>
    public class NRepositoryContext : IDisposable
    {
        private readonly MongoCollectionSettings _collectionSettings = new MongoCollectionSettings
        {
            AssignIdOnInsert = true,
            WriteConcern = WriteConcern.Acknowledged
        };

        private readonly MongoDatabaseSettings _databaseSettings = new MongoDatabaseSettings();

        private readonly Dictionary<Type, IBsonSerializer> _registeredSerializer =
            new Dictionary<Type, IBsonSerializer>();

        /// <summary>
        ///     This object track changes of the documents within the current context. This object can be shared
        ///     among many <see cref="NRepositoryContext" /> to provide a consistent caching mechanism.
        /// </summary>
        [NotNull] private NDocumentStateManager _documentStateManager;

        private NDocumentCacheCleaner _documentCacheCacheCleaner;
        /// <summary>
        /// Creates new repository context object for the specified arguments.
        /// </summary>
        /// <param name="mongoClient">The mongo client object used to communicate with MongDB server.</param>
        /// <param name="databaseName">The database name to which the context should associate.</param>
        /// <param name="stateManager">The document state manager used to track documents.</param>
        protected NRepositoryContext([NotNull] IMongoClient mongoClient, [NotNull] string databaseName,
            [NotNull] NDocumentStateManager stateManager)
        {
            if (mongoClient == null) throw new ArgumentNullException(nameof(mongoClient));
            if (databaseName == null) throw new ArgumentNullException(nameof(databaseName));
            if (stateManager == null) throw new ArgumentNullException(nameof(stateManager));

            MongoDatabase = mongoClient.GetDatabase(databaseName, _databaseSettings);
            DatabaseName = databaseName;
            _documentStateManager = stateManager;
            _documentCacheCacheCleaner = new NDocumentCacheCleaner(stateManager);
            RegisterCallbacks();
        }

        /// <summary>
        ///     Gets direct access to the Mongo database object.
        /// </summary>
        protected IMongoDatabase MongoDatabase { get; set; }

        /// <summary>
        ///     Gets <see cref="NDocumentStateManager" /> object used by the current <see cref="NRepositoryContext" />.
        /// </summary>
        internal NDocumentStateManager DocumentStateManager => _documentStateManager;

        /// <summary>
        ///     Gets the database name of the currently open database.
        /// </summary>
        public string DatabaseName { get; }

        /// <summary>
        ///     Gets a MongoUrl from app.config file. If this setting is not found then default url is provided.
        /// </summary>
        public static MongoUrl ConfiguredUrl
        {
            get
            {
                try
                {
                    return new MongoUrl(ConfigurationManager.ConnectionStrings["MongoServerSettings"].ConnectionString);
                }
                catch (Exception e)
                {
                    TraceLog.WriteWarning(
                        $"Unable to parse configure connection string because of '{e.Message}'. Using default which is 'mongodb://localhost/'.");
                    return new MongoUrl("mongodb://localhost/");
                }
            }
        }

        private void RegisterCallbacks()
        {
            var serializerRegistry = MongoDatabase.Settings.SerializerRegistry;
            foreach (var docType in SupportedDocumentTypes)
            {
                var nserializer = (serializerRegistry.GetSerializer(docType)) as NDocumentSerializer;
                if (nserializer != null)
                {
                    nserializer.AfterDocumentDeserialization = OnAfterDocumentDeserializationCallback;
                    nserializer.BeforeDocumentDeserialization = OnBeforeDocumentDeserializationCallback;
                    _registeredSerializer.Add(docType, nserializer);
                }
            }
        }

        /// <summary>
        ///     Creates a new RepositoryManager object with a connection string obtained from app.config file.
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        ///     To use settings from app.config file the following block should be added:
        ///     <connectionStrings>
        ///         <add name="MongoServerSettings" connectionString="mongodb://localhost/NetfoxDetective" />
        ///     </connectionStrings>
        ///     Connection string consist of protocol,server address and optionally database name.
        /// </remarks>
        public static NRepositoryContext Create(string databaseName, NDocumentStateManager documentStateManager = null)
        {
            if (databaseName == null) throw new ArgumentNullException(nameof(databaseName));
            var url = ConfiguredUrl;
            return Create(url, databaseName, documentStateManager);
        }

        /// <summary>
        ///     Creates a repository from the provided server url and database name.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="databaseName"></param>
        /// <param name="documentStateManager"></param>
        /// <returns></returns>
        public static NRepositoryContext Create(MongoUrl url, string databaseName,
            NDocumentStateManager documentStateManager = null)
        {
            if (url == null) throw new ArgumentNullException(nameof(url));
            if (databaseName == null) throw new ArgumentNullException(nameof(databaseName));            
            var client = new MongoClient(url);
            return new NRepositoryContext(client, databaseName, documentStateManager ?? new NDocumentStateManager());
        }

        /// <summary>
        ///     Gets a collection of the specified name. When creating a collection it uses default collection settings that
        ///     ensures that if no id is provided then a new one will be generated.
        /// </summary>
        /// <typeparam name="TDocument"></typeparam>
        /// <param name="collectionName"></param>
        /// <returns></returns>
        private NDocumentSet<TDocument> Collection<TDocument>(string collectionName) where TDocument : class, new() =>
            new NDocumentSet<TDocument>(this,
                MongoDatabase.GetCollection<TDocument>(collectionName, _collectionSettings), collectionName,
                Expression.Empty());

        /// <summary>
        ///     Gets the <see cref="NDocumentSet{TDocument}" /> instance for the documents of the specified type.
        /// </summary>
        /// <typeparam name="TDocument">A type of the documwents.</typeparam>
        /// <returns><see cref="NDocumentSet{TDocument}" /> instance for the documents of the specified type.</returns>
        public NDocumentSet<TDocument> Collection<TDocument>() where TDocument : class, new() =>
            new NDocumentSet<TDocument>(this,
                MongoDatabase.GetCollection<TDocument>(typeof (TDocument).Name, _collectionSettings),
                typeof (TDocument).Name, Expression.Empty());

        /// <summary>
        ///     Gets the <see cref="NDocumentSet" /> instance for the documents of the specified type.
        /// </summary>
        /// <returns><see cref="NDocumentSet" /> instance for the documents of the specified type.</returns>
        public NDocumentSet Collection(Type documentType)
        {
            var method = typeof(NRepositoryContext).GetMethod(nameof(Collection), new Type[] { });
            var generic = method.MakeGenericMethod(documentType);
            return generic.Invoke(this, new object[] { }) as NDocumentSet;
        }


        private NObservableCollection<TDocument> ObservableCollection<TDocument>(string collectionName)
            where TDocument : class, new() =>
                new NObservableCollection<TDocument>(this,
                    MongoDatabase.GetCollection<TDocument>(collectionName, _collectionSettings), collectionName);

        /// <summary>
        /// Gets the observable collection for the specified document type.
        /// </summary>
        /// <typeparam name="TDocument">A type of the document.</typeparam>
        /// <returns>An observable collection for the specified document type.</returns>
        public NObservableCollection<TDocument> ObservableCollection<TDocument>() where TDocument : class, new() =>
            new NObservableCollection<TDocument>(this,
                MongoDatabase.GetCollection<TDocument>(typeof (TDocument).Name, _collectionSettings),
                typeof (TDocument).Name);

        /// <summary>
        ///     Tries to fetch document from database according provided db reference.
        /// </summary>
        /// <param name="oid">An ObjectId that uniquelly refers to the object in Mongo Database.</param>
        /// <returns>A new object for DbObjectRef or null if DbObjectRef is null or does not refer to a valid object.</returns>
        public async Task<TDocument> FindObjectByIdAsync<TDocument>(ObjectId oid) where TDocument : class
        {
            var collection = MongoDatabase.GetCollection<TDocument>(typeof (TDocument).Name);
            return await collection.FindOneByIdAsync(oid).ConfigureAwait(false);
        }

        /// <summary>
        ///     Tries to fetch document from database according provided db reference.
        /// </summary>
        /// <param name="oids">A collection of ObjectId values to be resolved.</param>
        /// <returns>A (possible empty) collection of objects with the specified ObjectIds.</returns>
        public async Task<IEnumerable<TDocument>> FindObjectsByIdsAsync<TDocument>(IEnumerable<ObjectId> oids)
            where TDocument : class
        {
            var collection = MongoDatabase.GetCollection<TDocument>(typeof (TDocument).Name);
            return await collection.FindManyByIdsAsync(oids).ConfigureAwait(false);
        }

        /// <summary>
        ///     This methods is called beforedeserialization of the document. The result of this method is
        ///     an instance of object that should be filled with deserialized data. This instance can be either
        ///     newly created or read from the cache. Deserializer replace values of fields that are part of the
        ///     command's result. Thus it can be used for loading new object from DB as well as for reloading some of its values.
        /// </summary>
        /// <param name="oid"></param>
        /// <param name="documentType"></param>
        /// <returns></returns>
        private object OnBeforeDocumentDeserializationCallback(ObjectId oid, Type documentType)
        {
            var document = _documentStateManager.GetDocument(oid) ?? DocumentFactory.CreateNew(documentType, oid);
            Debug.Assert(oid != ObjectId.Empty, "oid cannot be empty");
            _documentStateManager.SetDocumentPropertyTracking(oid, false);
            return document;
        }

        /// <summary>
        /// Delegate that is used to informa user of <see cref="NRepositoryContext"/> about newly loaded document. 
        /// It is possible to perform custom initialization of the loaded document in this handler. 
        /// </summary>
        /// <param name="oid">>ObjectId value of the loaded document.</param>
        /// <param name="documentType">The type of document.</param>
        /// <param name="document">The document loaded from the database.</param>
        public delegate void DocumentLoadedHandler(ObjectId oid, [NotNull] Type documentType, [NotNull] object document);

        /// <summary>
        /// Called when document is loaded from the database. This event can be used for perform custom intialization of the document.
        /// </summary>
        public event DocumentLoadedHandler OnDocumentLoaded;

        /// <summary>
        ///     This is called after deserialization of the object. Such object should be tracked with
        ///     <see cref="DocumentState.Unchanged" /> state.
        /// </summary>
        /// <param name="oid">ObjectId value of the loaded document.</param>
        /// <param name="documentType">The type of document.</param>
        /// <param name="document">The document loaded from the database.</param>
        private void OnAfterDocumentDeserializationCallback(ObjectId oid, [NotNull] Type documentType,
            [NotNull] object document)
        {
            if (documentType == null) throw new ArgumentNullException(nameof(documentType));
            if (document == null) throw new ArgumentNullException(nameof(document));

            this.OnDocumentLoaded?.Invoke(oid, documentType, document);

            var entry = _documentStateManager.AddOrGetExistingStateEntry(oid, documentType, document);
            _documentStateManager.ChangeDocumentState(entry, DocumentState.Unchanged);
            _documentStateManager.SetDocumentPropertyTracking(oid, true);
        }

        /// <summary>
        ///     Deletes database of the given name from Mongo server specified by the url parameter.
        /// </summary>
        /// <param name="url">An URL representing a connection string to the target Mongo Server.</param>
        /// <param name="databaseName">A name of the database to delete.</param>
        public static async Task DeleteDatabaseAsync(MongoUrl url, string databaseName)
        {
            var settings = new MongoClientSettings
            {
                Server = url.Server,
                ConnectionMode = ConnectionMode.Direct,
                MaxConnectionPoolSize = 10,
                WaitQueueSize = 10000
            };
            var client = new MongoClient(settings);
            await client.DropDatabaseAsync(databaseName).ConfigureAwait(false);
        }
        /// <summary>
        /// Deletes database of the given name from the Mongo server specified in App.config or local one. 
        /// </summary>
        /// <param name="databaseName"></param>
        /// <returns></returns>
        public static async Task DeleteDatabaseAsync(string databaseName)
        {
            await DeleteDatabaseAsync(ConfiguredUrl, databaseName).ConfigureAwait(false);
        }

        /// <summary>
        ///     Gets a NDocumentEntry object for the given document providing access to information about the document and the
        ///     ability to perform actions on the document.
        /// </summary>
        /// <typeparam name="TDocument">The type of the document.</typeparam>
        /// <param name="document">A document object.</param>
        /// <returns>NDocumentEntry object for the given document, or null if document is not associated with any context.</returns>
        public NDocumentEntry<TDocument> Entry<TDocument>(TDocument document)
            where TDocument : class, new()
        {
            var key = (document as IDocument)?.Id ?? ObjectId.Empty;
            NDocumentStateEntry documentStateEntry;
            _documentStateManager.TryGetDocumentStateEntry(key, out documentStateEntry);
            return new NDocumentEntry<TDocument>(this, documentStateEntry, document);
        }
        /*
        internal NDocumentEntry TrackObject(NDocumentSet documentSet, object document, DocumentState state)
        {
            var key = (document as IDocument)?.Id ?? ObjectId.Empty;
            var documentStateEntry = _documentStateManager.AddOrGetExistingStateEntry(key, documentSet.ElementType, document, state);
            return NDocumentEntry.Create(this, documentStateEntry, document);
        }
        */
        internal NDocumentEntry<TDocument> TrackObject<TDocument>(NDocumentSet<TDocument> documentSet,
            TDocument document, DocumentState state)
            where TDocument : class, new()
        {
            var key = (document as IDocument)?.Id ?? ObjectId.Empty;
            var documentStateEntry = _documentStateManager.AddOrGetExistingStateEntry(key, typeof (TDocument), document,
                state);
            return new NDocumentEntry<TDocument>(this, documentStateEntry, document);
        }

        /// <summary>
        /// Reloads the specified document from the MongoDb. If the document was modified than all 
        /// changes will be overwritten with values from the database.
        /// </summary>
        /// <typeparam name="TDocument">The type of the document.</typeparam>
        /// <param name="documentEntry">The document entry specifying document to reload.</param>
        public async Task ReloadDocumentAsync<TDocument>(NDocumentEntry<TDocument> documentEntry)
            where TDocument : class, new()
        {
            NDocumentStateEntry stateEntry;
            if (_documentStateManager.TryGetDocumentStateEntry(documentEntry.Id, out stateEntry))
            {
                var result =
                    await
                        DbReloadDocumentAsync<TDocument>(documentEntry.DocumentType.Name, documentEntry.DocumentType,
                            stateEntry).ConfigureAwait(false);
            }
        }
        /// <summary>
        /// Saves all changes made to the object in the current context till last save.
        /// </summary>
        /// <returns>A nunber of hanges persisted to the database.</returns>
        public virtual async Task<int> SaveChangesAsync()
        {
            var savedCount = 0;
            // TODO: improve performance, a lot of unecessary LINQ and copying is here...
            var addedGroups =
                (from a in _documentStateManager.GetDocumentStateEntries(DocumentState.Added) group a by a.DocumentType)
                    .ToArray();
            foreach (var addedGroup in addedGroups)
            {
                var slices = addedGroup.Slice(1000);
                foreach (var slice in slices)
                {
                    var docs = slice.ToArray();
                    var failedDocs =
                        await DbInsertDocumentsAsync(addedGroup.Key.Name, addedGroup.Key, docs).ConfigureAwait(false);
                    var commitedDocs = docs.Except(failedDocs.Select(x => x.StateEntry)).ToArray();
                    foreach (var entry in commitedDocs)
                    {
                        _documentStateManager.ChangeDocumentState(entry, DocumentState.Unchanged);
                    }
                    savedCount += commitedDocs.Count();
                }
            }

            var modifiedGroups =
                (from a in _documentStateManager.GetDocumentStateEntries(DocumentState.Modified)
                    group a by a.DocumentType).ToArray();
            foreach (var modifiedGroup in modifiedGroups)
            {
                var slices = modifiedGroup.Slice(1000);
                foreach (var slice in slices)
                {
                    var docs = slice.ToArray();
                    var failedDocs =
                        await
                            DbUpdateDocumentsAsync(modifiedGroup.Key.Name, modifiedGroup.Key, docs)
                                .ConfigureAwait(false);
                    var commitedDocs = docs.Except(failedDocs.Select(x => x.StateEntry)).ToArray();
                    foreach (var entry in commitedDocs)
                    {
                        _documentStateManager.ChangeDocumentState(entry, DocumentState.Unchanged);
                    }
                    savedCount += commitedDocs.Count();
                }
            }

            var deletedGroups =
                (from a in _documentStateManager.GetDocumentStateEntries(DocumentState.Deleted)
                    group a by a.DocumentType).ToArray();
            foreach (var deleteGroup in deletedGroups)
            {
                var slices = deleteGroup.Slice(1000);
                foreach (var slice in slices)
                {
                    var docs = slice.ToArray();
                    var failedDocs =
                        await DbDeleteDocumentsAsync(deleteGroup.Key.Name, deleteGroup.Key, docs).ConfigureAwait(false);
                    var commitedDocs = docs.Except(failedDocs.Select(x => x.StateEntry)).ToArray();
                    foreach (var entry in commitedDocs)
                    {
                        _documentStateManager.ChangeDocumentState(entry, DocumentState.Detached);
                    }
                    savedCount += commitedDocs.Count();
                }
            }

            return savedCount;
        }

        /// <summary>
        ///     Saves changes to the document object specified by NDocumentEntry instance.
        /// </summary>
        /// <typeparam name="TDocument"></typeparam>
        /// <param name="documentEntry">A NDocumentEntry instace that describes the document to be saved.</param>
        /// <returns>true if the document was saved; false if the document was not saved because of its state or due to error.</returns>
        public async Task<bool> SaveChangesAsync<TDocument>(NDocumentEntry<TDocument> documentEntry)
            where TDocument : class, new()
        {
            NDocumentStateEntry entry;
            if (!_documentStateManager.TryGetDocumentStateEntry(documentEntry.Id, out entry)) return false;

            switch (entry.State)
            {
                case DocumentState.Added:
                {
                    var failed =
                        await DbInsertDocumentsAsync(entry.CollectionName, entry.DocumentType, new[] {entry});
                    if (failed.Any()) return false;
                    _documentStateManager.ChangeDocumentState(entry, DocumentState.Unchanged);
                    return true;
                }
                case DocumentState.Deleted:
                {
                    var failed =
                        await DbDeleteDocumentsAsync(entry.CollectionName, entry.DocumentType, new[] {entry});
                    if (failed.Any()) return false;
                    _documentStateManager.ChangeDocumentState(entry, DocumentState.Detached);
                    return true;
                }
                case DocumentState.Modified:
                {
                    var failed =
                        await DbUpdateDocumentsAsync(entry.CollectionName, entry.DocumentType, new[] {entry});
                    if (failed.Any()) return false;
                    _documentStateManager.ChangeDocumentState(entry, DocumentState.Unchanged);
                    return true;
                }
                default: // DocumentState.Unchanged or DocumentState.Detached 
                    return false;
            }
        }

        /// <summary>
        ///     Inserts a collection of objects in the database.
        /// </summary>
        /// <param name="collectionName"></param>
        /// <param name="documentType"></param>
        /// <param name="entry"></param>
        /// <returns>
        ///     Returns null if an error occured during preparing the insert command. It means that any object has not been
        ///     inserted.
        ///     Returns an enumerable of objects that cannot be inserted. For each object a reason is specified.
        ///     If all objects have been inserted then the result is an empty collection.
        /// </returns>
        private async Task<IEnumerable<CommandErrorEntry>> DbInsertDocumentsAsync(string collectionName,
            Type documentType, NDocumentStateEntry[] entry)
        {
            try
            {
                var serializer = GetSerializerForDocumentType(documentType);
                if (serializer == null)
                    throw new ArgumentNullException(nameof(documentType),
                        $"Serializer for the provided type '{documentType}' has not been registered in the current Context.");
                var documents = entry.Select(e => e.Document?.ToBsonDocument(documentType, serializer));
                var docarray = new BsonArray(documents);
#pragma warning disable 618
                var insertCmd = new BsonDocument(
                    new BsonElement("insert", BsonValue.Create(collectionName)),
                    new BsonElement("documents", docarray)
                    );
#pragma warning restore 618                
                var result = await MongoDatabase.RunCommandAsync(new BsonDocumentCommand<BsonDocument>(insertCmd));
                BsonElement writeErrors;
                if (result != null && result.TryGetElement("writeErrors", out writeErrors))
                {
                    return CollectErrors(collectionName, entry,
                        writeErrors.Value.AsBsonArray.Select(x => x.AsBsonDocument));
                }


                return new CommandErrorEntry[] {};
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                throw;
            }
        }

        private IBsonSerializer GetSerializerForDocumentType(Type documentType)
        {
            IBsonSerializer serializer;
            return _registeredSerializer.TryGetValue(documentType, out serializer) ? serializer : null;
        }

        private async Task<IEnumerable<CommandErrorEntry>> DbUpdateDocumentsAsync(string collectionName,
            Type documentType, NDocumentStateEntry[] entries)
        {
            try
            {
                //TODO(NRepositoryContext.DbUpdateDocumentAsync) Refine implementation of this method, so that only modified properties are updated.
                // Currently the whole object is send to the server to be replaced with new values. It is possible to detect changes in properties and update
                // only these properties.
                var serializer = GetSerializerForDocumentType(documentType);
                if (serializer == null)
                    throw new ArgumentNullException(nameof(documentType),
                        $"Serializer for the provided type '{documentType}' has not been registered in the current Context.");

                var documents = entries.Select(e =>
                    new BsonDocument(new Dictionary<string, object>
                    {
                        ["q"] = new BsonDocument(new BsonElement("_id", BsonValue.Create(e.Key))),
                        ["u"] = e.Document?.ToBsonDocument(documentType, serializer)
                    }));
                var docarray = new BsonArray(documents);

#pragma warning disable 618
                var updateCmd = new BsonDocument(
                    new BsonElement("update", BsonValue.Create(collectionName)),
                    new BsonElement("updates", docarray)
                    );
#pragma warning restore 618
                var result = await MongoDatabase.RunCommandAsync(new BsonDocumentCommand<BsonDocument>(updateCmd));
                BsonElement writeErrors;
                if (result != null && result.TryGetElement("writeErrors", out writeErrors))
                {
                    return CollectErrors(collectionName, entries,
                        writeErrors.Value.AsBsonArray.Select(x => x.AsBsonDocument));
                }

                return new CommandErrorEntry[] {};
            }
            catch (Exception e)
            {
                TraceLog.WriteError(e.Message);
                throw;
            }
        }

        private async Task<IEnumerable<CommandErrorEntry>> DbDeleteDocumentsAsync(string collectionName,
            Type documentType, NDocumentStateEntry[] entry)
        {
            try
            {
                //TODO(NRepositoryContext.DbDeleteDocumentsAsync) Test implementation of delete!

                var documents = entry.Select(e =>
                    new BsonDocument(new Dictionary<string, object>
                    {
                        ["q"] = new BsonDocument(new BsonElement("_id", BsonValue.Create(e.Key))),
                        ["limit"] = BsonValue.Create(0)
                    }));
                var docarray = new BsonArray(documents);

#pragma warning disable 618
                var updateCmd = new BsonDocument(new BsonElement("delete", BsonValue.Create(collectionName)),
                    new BsonElement("deletes", docarray));
#pragma warning restore 618
                var result = await MongoDatabase.RunCommandAsync(new BsonDocumentCommand<BsonDocument>(updateCmd));
                BsonElement writeErrors;
                if (result != null && result.TryGetElement("writeErrors", out writeErrors))
                {
                    return CollectErrors(collectionName, entry,
                        writeErrors.Value.AsBsonArray.Select(x => x.AsBsonDocument));
                }

                return new CommandErrorEntry[] {};
            }
            catch (Exception e)
            {
                TraceLog.WriteError(e.Message);
                throw;
            }
        }


        // db.Bar.findAndModify({ findAndModify:"Bar", query : {_id :  ObjectId("55b6207e108ed01ca49176b9")}, update : {}})
        private async Task<IEnumerable<CommandErrorEntry>> DbReloadDocumentAsync<TDocument>(
            [NotNull] string collectionName, [NotNull] Type documentType, [NotNull] NDocumentStateEntry entry)
            where TDocument : class, new()
        {
            if (collectionName == null) throw new ArgumentNullException(nameof(collectionName));
            if (documentType == null) throw new ArgumentNullException(nameof(documentType));
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            try
            {
                // reload command is based on findAndModify using empty update statement, 
                // TODO(DbReloadDocumentAsync): check if "findAndModify" command is the best way of doing reload
                var reloadCmd = new BsonDocument(new[]
                {
                    new BsonElement("findAndModify", BsonValue.Create(collectionName)),
                    new BsonElement("query", new BsonDocument(new BsonElement("_id", BsonValue.Create(entry.Key)))),
                    new BsonElement("update", new BsonDocument())
                } as IEnumerable<BsonElement>);
                // run the command and wait for the results
                var result = await MongoDatabase.RunCommandAsync(new BsonDocumentCommand<BsonDocument>(reloadCmd));
                // result should not be null, if so, then exception is thrown
                if (result == null) throw new MongoCommandException(null, "Cannot obtain result", reloadCmd);

                BsonElement writeErrors;
                // does operation end with errors?
                if (result.TryGetElement("writeErrors", out writeErrors))
                {
                    // collect errors and return them in CommandErrorEntry enumerable
                    return CollectErrors(collectionName, new[] {entry},
                        writeErrors.Value.AsBsonArray.Select(x => x.AsBsonDocument));
                }
                // everything went ok, we should have a result:
                var val = result["value"];
                var doc = val?.AsBsonDocument;
                BsonSerializer.Deserialize<TDocument>(doc);
                _documentStateManager.ChangeDocumentState(entry, DocumentState.Unchanged);
                return new CommandErrorEntry[] {};
            }
            catch (Exception e)
            {
                TraceLog.WriteError(e.Message);
                throw;
            }
        }

        private static IEnumerable<CommandErrorEntry> CollectErrors(string collectionName,
            [NotNull] NDocumentStateEntry[] entry, [NotNull] IEnumerable<BsonDocument> errors)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            if (errors == null) throw new ArgumentNullException(nameof(errors));

            return from error in errors
                let index = error.GetValue("index").ToInt32()
                let code = error.GetValue("code").ToInt32()
                let msg = error.GetValue("errmsg").ToString()
                select new CommandErrorEntry(index, code, msg, entry[index]);
        }
        /// <summary>
        /// Creates a new document using its parameterless constructor.
        /// </summary>
        /// <typeparam name="TDocument"></typeparam>
        /// <returns></returns>
        public TDocument CreateObject<TDocument>() where TDocument : class, new()
        {
            var type = typeof (TDocument);
            return !type.IsDefined(typeof (IDocument)) ? null : new TDocument();
        }
        /// <summary>
        /// Gets the statistics about the usage of the current <see cref="NRepositoryContext"/>.
        /// </summary>
        /// <returns>The statistics about the usage of the current <see cref="NRepositoryContext"/>.</returns>
        public ContextStatistics GetStatistics()
        {
            return new ContextStatistics
            {
                DatabaseName = DatabaseName,
                AddedDocuments = _documentStateManager?.GetDocumentStateEntriesCount(DocumentState.Added) ?? 0,
                ModifiedDocuments = _documentStateManager?.GetDocumentStateEntriesCount(DocumentState.Modified) ?? 0,
                DeletedDocuments = _documentStateManager?.GetDocumentStateEntriesCount(DocumentState.Deleted) ?? 0,
                UnchangedDocuments = _documentStateManager?.GetDocumentStateEntriesCount(DocumentState.Unchanged) ?? 0,
                UnchangedAllDocuments = _documentStateManager?.GetDocumentCacheSize() ?? 0,
                LastCleanUp = _documentStateManager?.LastCleanUp ?? DateTime.MinValue,
                TotalReclaimed = _documentStateManager?.TotalReclaimedDocuments ?? 0L,
                TotalCleanUpTime = _documentStateManager?.TotalCleanupTime ?? TimeSpan.Zero,
                FullCleanUpExecCount = _documentStateManager?.FullCleanUpExecCount ?? 0L,
                PartialCleanUpExecCount = _documentStateManager?.PartialCleanUpExecCount ?? 0L
            };
        }

        internal struct CommandErrorEntry
        {
            internal int Code;
            internal int Index;
            internal string Message;
            internal NDocumentStateEntry StateEntry;

            internal CommandErrorEntry(int index, int code, string errormsg, NDocumentStateEntry entry)
            {
                Index = index;
                Code = code;
                Message = errormsg;
                StateEntry = entry;
            }
        }

        #region Registration of Document types from assemblies
        /// <summary>
        /// Register document types that implementes <see cref="IDocument"/> interface in the current context.
        /// </summary>
        /// <param name="assembly">An assembly inspected for supported document types to be registered.</param>
        /// <remarks>
        /// Registration of documents is necessary as MongoDB driver requires that all custom data types have to 
        /// be registered before Serialization infrastructure is constructed. Without registration it would not be possible
        /// to correctly serialize/deserialize the documents.
        /// </remarks>
        public static void RegisterDocumentTypes(Assembly assembly)
        {
            if (RegisteredAssemblies.Contains(assembly.GetName().Name)) return;
            RegisteredAssemblies.Add(assembly.GetName().Name);

            foreach (var docType in CachedTypeInfo.GetDocumentTypes(assembly))
            {
                var serializer =
                    Activator.CreateInstance(typeof (NDocumentSerializer<>).MakeGenericType(docType.AsType()));
                ((NDocumentSerializer) serializer).BeforeDocumentDeserialization = null;
                ((NDocumentSerializer) serializer).AfterDocumentDeserialization = null;
                BsonSerializer.RegisterSerializer(docType.AsType(), (IBsonSerializer) serializer);
                SupportedDocumentTypes.Add(docType);
            }
        }

        /// <summary>
        ///     Static constructor that is called before repository context can be used. It registers custom serializers
        ///     as other better ways to do this do not work (perhaps due to a bug in PostSharp?)
        /// </summary>
        static NRepositoryContext()
        {
            RegisterDocumentTypes(Assembly.GetEntryAssembly());
            foreach (var assemblyName in Assembly.GetEntryAssembly().GetReferencedAssemblies())
            {
                var assembly = Assembly.Load(assemblyName);
                RegisterDocumentTypes(assembly);
            }
        }

        private static readonly HashSet<string> RegisteredAssemblies = new HashSet<string>();
        private static readonly HashSet<Type> SupportedDocumentTypes = new HashSet<Type>();

        #endregion

        #region IDisposable Support

        private bool _disposedValue; // To detect redundant calls

        /// <summary>
        /// Dispose the current object by releasing managed resources
        /// </summary>
        /// <param name="disposing">true if resources should be disposed; false otherwise.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _documentCacheCacheCleaner.Dispose();
                    _documentStateManager.Dispose();
                }
                _documentStateManager = null;
                _documentCacheCacheCleaner = null;
                _disposedValue = true;
            }
        }
        /// <summary>
        /// Dispose the current object by releasing managed resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        #endregion
    }
}