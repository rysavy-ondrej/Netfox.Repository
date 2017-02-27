//
//  IDocument.cs
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
using System.ComponentModel;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Netfox.Repository.Attributes;

#endregion

namespace Netfox.Repository
{
    /// <summary>
    ///     An interface which should be implemented all data object that can be managed by <see cref="NRepositoryContext" />.
    /// </summary>
    /// <remarks>
    ///     This interface shold be directly implemented by data objects or it is possible to
    ///     apply aspect that implements this interface by annotating the data class by <see cref="NDocumentAttribute" />.
    /// </remarks>
    /// 
    /// <example>
    ///     This example shows the use of <see cref="NDocumentAttribute" /> to implement the interface by aspect.
    ///     <code>
    /// [NDocument]
    /// public class Message 
    /// { 
    ///     [NReference]
    ///     public User Sender { get; set; }
    /// 
    ///     [NCollection(typeof(User)] 
    ///     public ICollectiony&lt;User&gt; Receivers { get; set; }
    ///     
    ///     [NValue]
    ///     public string Text { get; set; }
    /// } 
    /// </code>
    /// </example>
    /// 
    /// <example>
    /// It is also posisible to provide custom implementation of <see cref="IDocument"/> interface:
    ///     <code>
    /// 
    /// public class Message  : IDocument
    /// { 
    ///     public Message()
    ///     {
    ///         this.ObjectId = ObjectId.GenerateNewId();
    ///         this.Navigable = new Dictionary&lt;string,object&gt;();    
    ///     }
    /// 
    ///     public ObjectId { get; set; }
    ///     
    ///     public IDictionary&lt;string,object&gt; Navigable { get; private set; }
    /// 
    ///     public event PropertyChangedEventHandler ControlledPropertyChanged;
    /// 
    ///     public void SetPropertyValue(string propertyName, object value)
    ///     {
    ///         var accessor = ObjectAccessor.Create(this);
    ///         accessor[propertyName] = value;
    ///     }
    /// 
    ///     public object GetPropertyValue(string propertyName)
    ///     {    
    ///         var accessor = ObjectAccessor.Create(this);
    ///         return accessor[propertyName];
    ///     }
    /// 
    ///     private User _sender;
    ///     [NReference]
    ///     public User Sender 
    ///     { 
    ///         get
    ///         {
    ///             return this._sender;
    ///         }
    ///         set 
    ///         {
    ///             if (value != this._sender)
    ///             {   
    ///                 this._sender = value;
    ///                 ControlledPropertyChanged?.Invoke(Instance, new PropertyChangedEventArgs(nameof(Sender)));
    ///             }
    ///         }
    ///     }
    /// 
    ///     private List&lt;Users&gt; _receivers;
    ///     [NCollection(typeof(User)] 
    ///     public ICollection&lt;User&gt; Receivers
    ///     { 
    ///         get
    ///         {
    ///             return this._receivers;
    ///         }
    ///         set 
    ///         {
    ///         }
    ///      } 
    ///     string _text;    
    ///     [NValue]
    ///     public string Text 
    ///     { 
    ///         get
    ///         {
    ///             return test;
    ///         }
    ///         set 
    ///         {
    ///             if (value != this._text)
    ///             {   
    ///                 this._text = value;
    ///                 ControlledPropertyChanged?.Invoke(Instance, new PropertyChangedEventArgs(nameof(Text)));
    ///             }
    ///         }
    ///     }
    /// } 
    /// </code>
    /// </example>

    public interface IDocument
    {
        /// <summary>
        ///     Defines a unique identification of the document.
        /// </summary>
        [BsonRepresentation(BsonType.ObjectId)]
        ObjectId Id { get; set; }

        /// <summary>
        ///     Handles references of navigable properties.
        /// </summary>
        IDictionary<string, object> Navigable { get; }

        /// <summary>
        ///     Sets the value of the controlled property in the target document.
        /// </summary>
        /// <param name="propertyName">A name of the controlled property.</param>
        /// <param name="value">A value to be write to the controlled property</param>
        /// <exception cref="ArgumentOutOfRangeException">if propertyName is not valid name of the document property</exception>
        /// <exception cref="ArgumentException">if value cannot be assigned to the target property</exception>
        void SetPropertyValue(string propertyName, object value);

        /// <summary>
        ///     Gets the value of controlled property in the target document.
        /// </summary>
        /// <param name="propertyName">A name of the controlled property.</param>
        /// <returns>Value of the controlled property.</returns>
        /// <exception cref="ArgumentOutOfRangeException">if propertyName is not valid name of the document property</exception>
        object GetPropertyValue(string propertyName);

        /// <summary>
        ///     Occurs when a controlled property value changes.
        /// </summary>
        event PropertyChangedEventHandler ControlledPropertyChanged;
    }
}