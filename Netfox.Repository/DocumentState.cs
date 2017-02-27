//
//  DocumentState.cs
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

namespace Netfox.Repository
{
    /// <summary>
    ///     Describes the state of a document. Note that is uses the same semantics as Entity Framework.
    /// </summary>
    /// <remarks>
    /// This enumeration is just a copy of EntityState enumeration from Entity Framework, see:
    ///     https://msdn.microsoft.com/en-us/library/system.data.entitystate(v=vs.110).aspx
    /// </remarks>
    [Flags]
    public enum DocumentState
    {
        /// <summary>
        ///     The document is being tracked by the context but does not yet exist in the database.
        /// </summary>
        Added = 4,

        /// <summary>
        ///     The document is being tracked by the context and exists in the database, but has been marked for deletion from the
        ///     database the next time SaveChanges is called.
        /// </summary>
        Deleted = 8,

        /// <summary>
        ///     The object exists but is not being tracked. An entity is in this state immediately after it has been created and
        ///     before it is added to the object context.
        /// </summary>
        Detached = 1,

        /// <summary>
        ///     The document is being tracked by the context and exists in the database, and some or all of its property values
        ///     have been modified.
        /// </summary>
        Modified = 16,

        /// <summary>
        ///     The document is being tracked by the context and exists in the database, and its property values have not changed
        ///     from the values in the database.
        /// </summary>
        Unchanged = 2
    }
}