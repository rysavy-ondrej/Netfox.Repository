//
//  NReferenceAttribute.cs
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

namespace Netfox.Repository.Attributes
{
    /// <summary>
    /// This attribute should be used on data object to annotate properties that are references.
    /// </summary>
    /// <example>
    /// Usage of <see cref="NReferenceAttribute"/> is shown in the following example:
    /// <code> 
    /// [NDocument]
    /// public class Message  
    /// { 
    ///     [NReference]
    ///     public User Sender { get; set; }
    /// 
    ///     [NCollection(typeof(User)] 
    ///     public ICollection{User} Receivers { get; set; }
    ///     
    ///     [NValue]
    ///     public string Text { get; set; }
    /// } 
    /// </code> 
    /// Here, Sender is a users that sent the message. It is a reference to the User object. 
    /// It is persisted as oid.  The Netfox.Repository performs this automatically when 
    /// the property is  annotated with <see cref="NReferenceAttribute"/>. 
    /// </example>
    [AttributeUsage(AttributeTargets.Property)]
    public class NReferenceAttribute : Attribute
    {
        /// <summary>
        /// Creates a new <see cref="NReferenceAttribute"/> instance. 
        /// </summary>
        public NReferenceAttribute()
        {
        }
    }
}