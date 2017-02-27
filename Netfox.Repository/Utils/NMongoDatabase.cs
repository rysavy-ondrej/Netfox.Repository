using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Netfox.Repository.Utils
{
    /// <summary>
    /// Describes the status of the mongo server.
    /// </summary>
    public enum NMongoServerStatus
    {
        /// <summary>
        /// Server was executed and it is currently initializing. 
        /// </summary>
        Initializing, 
        /// <summary>
        /// Servwers is running. It is replying to commands. 
        /// </summary>
        Running, 
        /// <summary>
        /// Servers does not run.
        /// </summary>
        Terminated
    }
    /// <summary>
    /// This class implementes a set of methods to control running mongo database server.
    /// </summary>
    public class NMongoDatabase
    {
        NMongoDatabase(MongoClient client, Process process)
        {
            this.MongoClient = client;
            this.Process = process;
        }
        /// <summary>
        /// Gets the process of the MongoDatabase server.
        /// </summary>
        public Process Process { get; private set; }
        /// <summary>
        /// 
        /// </summary>
        public IMongoClient MongoClient { get; private set; }

        /// <summary>
        /// Gets the folder in which the server will store all data.
        /// </summary>
        public DirectoryInfo DataFolder { get; private set; }

        /// <summary>
        /// Gets the port on which the server listens.
        /// </summary>
        public int ServerPort => this.MongoClient.Settings.Server.Port;
        /// <summary>
        /// Executes the mongo database server. 
        /// </summary>
        /// <param name="mongodProcess"></param>
        /// <param name="dataPath"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        public static NMongoDatabase Execute(string mongodProcess, string dataPath, int port)
        {
            var rootDataDirectory = Directory.CreateDirectory(dataPath);

            var processStartInfo = new ProcessStartInfo(mongodProcess, $"--port {port} --dbpath \"{dataPath}\"")
            {
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
#if DEBUG
                CreateNoWindow = true
#else
                    CreateNoWindow = false
#endif
            };
            var process = Process.Start(processStartInfo);
            var url = new MongoUrl($"mongodb://localhost:{port}");
            var client = new MongoClient(url);

            return new NMongoDatabase(client, process) { DataFolder = new DirectoryInfo(dataPath)};
        }


        /// <summary>
        /// Gets the current status of the server.
        /// </summary>
        public NMongoServerStatus Status
        {
            get
            {
                var mongodLockFile = new FileInfo(Path.Combine(this.DataFolder.FullName, "mongod.lock"));
                if (mongodLockFile.Exists && mongodLockFile.Length > 0) return NMongoServerStatus.Running;
                this.Process?.Refresh();
                if (this.Process == null || this.Process.HasExited) return NMongoServerStatus.Terminated;
                return NMongoServerStatus.Initializing;
            }
        }
        

        /// <summary>
        /// The shutdown command cleans up all database resources and then terminates the process. 
        /// </summary>
        public async Task ShutdownAsync()
        {
            var shutdownCommand = new BsonDocument(new Dictionary<string, object>() { { "shutdown", 1 } });
            try
            {
                await
                    this.MongoClient.GetDatabase("admin")
                        .RunCommandAsync(new BsonDocumentCommand<BsonDocument>(shutdownCommand));
            }
            catch { }         
        }
    }
}
