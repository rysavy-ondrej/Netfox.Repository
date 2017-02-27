//
//  TraceLog.cs
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netfox.Repository.Utils
{
    /// <summary>
    /// Provides a wrapper for logging methods.
    /// </summary>
    public static class TraceLog
    {

        /// <summary>
        /// Writes an error to the application log file.
        /// </summary>
        /// <param name="message">Log message.</param>
        /// <param name="memberName">A name of method calling this method.</param>
        /// <param name="sourceFilePath">Source file of the method calling this method.</param>
        /// <param name="sourceLineNumber">Line number in source file of the method when the call was made.</param>
        public static void WriteError(string message,
        [System.Runtime.CompilerServices.CallerMemberName] string memberName = "",
        [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = 0)
        {
            System.Diagnostics.Trace.TraceError($"{DateTime.Now}: {sourceFilePath}[{sourceLineNumber}].{memberName} : {message}");
        }

        /// <summary>
        /// Writes an information to the application log file.
        /// </summary>
        /// <param name="message">Log message.</param>
        /// <param name="memberName">A name of method calling this method.</param>
        /// <param name="sourceFilePath">Source file of the method calling this method.</param>
        /// <param name="sourceLineNumber">Line number in source file of the method when the call was made.</param>
        public static void WriteInformation(string message,
        [System.Runtime.CompilerServices.CallerMemberName] string memberName = "",
        [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = 0)
        {
            System.Diagnostics.Trace.WriteLine($"{DateTime.Now}: {sourceFilePath}[{sourceLineNumber}].{memberName} : {message}","Information");
        }

        /// <summary>
        /// Writes a warning to the application log file.
        /// </summary>
        /// <param name="message">Log message.</param>
        /// <param name="memberName">A name of method calling this method.</param>
        /// <param name="sourceFilePath">Source file of the method calling this method.</param>
        /// <param name="sourceLineNumber">Line number in source file of the method when the call was made.</param>
        public static void WriteWarning(string message,
        [System.Runtime.CompilerServices.CallerMemberName] string memberName = "",
        [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = 0)
        {
            System.Diagnostics.Trace.TraceWarning($"{DateTime.Now}: {sourceFilePath}[{sourceLineNumber}].{memberName} : {message}");
        }

        /// <summary>
        /// Writes a critical error to the application log file.
        /// </summary>
        /// <param name="message">Log message.</param>
        /// <param name="memberName">A name of method calling this method.</param>
        /// <param name="sourceFilePath">Source file of the method calling this method.</param>
        /// <param name="sourceLineNumber">Line number in source file of the method when the call was made.</param>
        public static void WriteCritical(string message,
        [System.Runtime.CompilerServices.CallerMemberName] string memberName = "",
        [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = 0)
        {
            System.Diagnostics.Trace.TraceError($"CRITICAL: {DateTime.Now}: {sourceFilePath}[{sourceLineNumber}].{memberName} : {message}");
        }
    }
}
