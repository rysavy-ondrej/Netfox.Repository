using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Netfox.Repository.Utils;

namespace Netfox.Repository.Internals
{
    // TODO: polish this class and introduce a solid terminology.
    internal class NDocumentCacheCleaner : IDisposable
    {
        /// <summary>
        /// Thread object that polls information from GC and executes cache cleanup procedure.
        /// </summary>
        private readonly Thread _gcInformationPollingThread;
        /// <summary>
        /// Reference to <see cref="NDocumentStateManager"/> to which the current instance belong.
        /// </summary>
        private readonly NDocumentStateManager _documentStateManager;


        /// <summary>
        /// Defines a lower bound for Document cache cleanup interval. Document cache will not be inspected for dead object
        /// sooner than the specified lower bound. This value represents miliseconds.
        /// </summary>
        private int _gcIntervalLowerBound = 5000;

        /// <summary>
        /// Defines a upper bound for Document cache cleanup interval. Document cache will be inspected for dead object
        /// sooner than the specified upper bound. This value represents miliseconds.
        /// </summary>
        private int _gcIntervalUpperBound = 10000;

        /// <summary>
        /// Keeps time information about the last clean up.
        /// </summary>
        private DateTime _lastCleanupTime = DateTime.MinValue;
        /// <summary>
        /// Gets or sets the upper bound of time on executing cache cleanup procedure. 
        /// </summary>
        internal TimeSpan CacheCleanUpUpperBound
        {
            get { return TimeSpan.FromMilliseconds(_gcIntervalUpperBound); }
            set
            {
                if (value.TotalMilliseconds < _gcIntervalLowerBound) throw new ArgumentOutOfRangeException(nameof(value),"GcTimeout cannot be less than its lower bound.");
                this._gcIntervalUpperBound = (int)value.TotalMilliseconds;
            }
        }
        /// <summary>
        /// Gets or sets the lower bound of time on executing cache cleanup procedure.
        /// </summary>
        internal TimeSpan CacheCleanUpLowerBound
        {
            get { return TimeSpan.FromMilliseconds(_gcIntervalLowerBound); }
            set
            {
                if (value.TotalMilliseconds > _gcIntervalUpperBound) throw new ArgumentOutOfRangeException(nameof(value), "GcTimeout cannot be greater than its upper bound.");
                this._gcIntervalLowerBound = (int)value.TotalMilliseconds;
            }
        }
        /// <summary>
        /// Creates a new instance of <see cref="NDocumentCacheCleaner"/> class.
        /// </summary>
        /// <param name="documentStateManager">Reference to <see cref="NDocumentStateEntry"/> object. The current object will perform operations on that object.</param>
        internal NDocumentCacheCleaner(NDocumentStateManager documentStateManager)
        {
            Int32.TryParse(ConfigurationManager.AppSettings["CacheCleanUpLowerBound"] ?? "10000", out _gcIntervalLowerBound);
            Int32.TryParse(ConfigurationManager.AppSettings["CacheCleanUpUpperBound"] ?? "60000", out _gcIntervalUpperBound);
            this._documentStateManager = documentStateManager;
            GC.RegisterForFullGCNotification(30, 30);

            _gcInformationPollingThread = new Thread(() =>
            {
                while (true)
                {
                    var status = GC.WaitForFullGCComplete(_gcIntervalUpperBound); 
                    if (status == GCNotificationStatus.Succeeded || status == GCNotificationStatus.Timeout)
                    {
                        PerformCleanUp(status);
                    }
                }
            });

            _gcInformationPollingThread.Start();
        }

        /// <summary>
        /// Executes cleanup procedure if at least <see cref="CacheCleanUpLowerBound"/> miliseconds elapsed from the last completed cleanup. Otherwise it returns immediately.
        /// </summary>
        private void PerformCleanUp(GCNotificationStatus status)
        {
            if (this._lastCleanupTime + TimeSpan.FromMilliseconds(this._gcIntervalLowerBound) < DateTime.Now)
            {
                TraceLog.WriteInformation($"Executing document cache inspection process, reason={status}.");
                this._documentStateManager.CleanUp(status == GCNotificationStatus.Succeeded);
                this._lastCleanupTime = DateTime.Now;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected void Dispose(bool disposing)
        {
            if (disposing)
            {
                _gcInformationPollingThread.Abort();
                GC.CancelFullGCNotification();
            }
        }
    }
}
