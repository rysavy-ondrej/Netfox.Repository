//
//  ContextStatistics.cs
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
using System.IO;

namespace Netfox.Repository
{
    /// <summary>
    /// Provides information and statistics about the usage of Repository Context.
    /// </summary>
    public class ContextStatistics
    {
        /// <summary>
        /// Name of associated database.
        /// </summary>
        public string DatabaseName { get; set; }
        /// <summary>
        /// Number of documents in DocumentState.Added state.
        /// </summary>
        public int AddedDocuments { get; internal set; }
        /// <summary>
        /// Number of documents in DocumentState.Modified state.
        /// </summary>
        public int ModifiedDocuments { get; internal set; }
        /// <summary>
        /// Number of documents in DocumentState.Deleted state.
        /// </summary>
        public int DeletedDocuments { get; internal set; }
        /// <summary>
        /// Number of documents in DocumentState.Unchaged state.
        /// </summary>
        public int UnchangedDocuments { get; internal set; }
        /// <summary>
        /// DateTime information when the last clean up (removing dead documents) was perfomed.
        /// </summary>
        public DateTime LastCleanUp { get; set; }
        /// <summary>
        /// Total number of documents reclaimed during all clean up runs.
        /// </summary>
        public long TotalReclaimed { get; set; }
        /// <summary>
        /// Total time spent by cleanin up the contex.
        /// </summary>
        public TimeSpan TotalCleanUpTime { get; set; }
        /// <summary>
        /// Total number of clean up runs.
        /// </summary>
        public long FullCleanUpExecCount { get; set; }
        /// <summary>
        /// Number of clean up runs that failed to run, e.g., because the system was too busy to run them.
        /// </summary>
        public long PartialCleanUpExecCount { get; set; }

        /// <summary>
        /// Number of all unchanged entries managed in the context including dead documents.
        /// </summary>
        public int UnchangedAllDocuments { get; set; }

        /// <summary>
        /// Prints the statistics to the specified writer.
        /// </summary>
        /// <param name="writer">A writer used to print the statistics.</param>
        public void Print(TextWriter writer)
        {
            writer.WriteLine($"Context, database: '{DatabaseName}'");
            writer.WriteLine("   Pending changes:");
            writer.WriteLine($"     Added documents:        {AddedDocuments}    ");
            writer.WriteLine($"     Modified documents:     {ModifiedDocuments}    ");
            writer.WriteLine($"     Deleted documents:      {DeletedDocuments}    ");
            writer.WriteLine($"   Cache Info:");
            var cacheLivePercent = UnchangedDocuments / (((double) UnchangedAllDocuments)/100);
            writer.WriteLine($"     Cached documents (L/*): {UnchangedDocuments} / {UnchangedAllDocuments} ({cacheLivePercent:N}%)  ");
            writer.WriteLine($"   Cache Cleaning Info:");
            writer.WriteLine($"     CleanUp Count (F/P):    {FullCleanUpExecCount} / {PartialCleanUpExecCount}    ");
            writer.WriteLine($"     Last Cleanup:           {LastCleanUp}    ");
            writer.WriteLine($"     Total Cleanup time:     {TotalCleanUpTime}    ");
            writer.WriteLine($"     Total Reclaimed:        {TotalReclaimed}    ");
        }
    }
}