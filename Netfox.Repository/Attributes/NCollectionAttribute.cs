using System;

namespace Netfox.Repository.Attributes
{
    /// <summary>
    /// This attribute should be used on data object to annotate properties that are collection of references.
    /// </summary>
    /// <example>
    /// Usage of <see cref="NCollectionAttribute"/> is shown in the following example:
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
    /// Here, Receivers is a set of users to which the message should be received. It is a collection of User objects
    /// that are persisted as a collection of references. The Netfox.Repository performs this automatically when 
    /// such collection is annotated with <see cref="NCollectionAttribute"/>. Note that type of the object in this collection 
    /// needs to be provided as the attribute argument.
    /// </example>
    [AttributeUsage(AttributeTargets.Property)]
    public class NCollectionAttribute : Attribute
    {
        /// <summary>
        ///     Creates a new NCollectionAttribute object for the given document type enumerated by the navigable collection
        ///     property.
        /// </summary>
        /// <param name="documentType"></param>
        public NCollectionAttribute(Type documentType)
        {
            DocumentType = documentType;
        }

        /// <summary>
        ///     Gets the type of the document enumerated by the navigable collection property.
        /// </summary>
        public Type DocumentType { get; }

        /// <summary>
        /// Creates a <see cref="NHashSet{TDocument}"/> instance for the specified <see cref="DocumentType"/>.
        /// </summary>
        /// <returns></returns>
        public object CreateHashSet()
        {
            return Activator.CreateInstance(typeof (NHashSet<>).MakeGenericType(DocumentType));
        }
    }
}