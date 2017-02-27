using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using FastMember;
using MongoDB.Bson;
using Netfox.Repository.Utils;
using PostSharp.Aspects;
using PostSharp.Aspects.Advices;
using PostSharp.Constraints;
using PostSharp.Extensibility;
using PostSharp.Reflection;

namespace Netfox.Repository.Attributes
{
    /// <summary>
    ///     This attribute should be used with POCO objects serialized to MongoDB. Using this attribute
    ///     persistent aspect is applied to the target classs.
    /// </summary>
    /// <remarks>
    ///     Persisntent aspect adds the following:
    ///     (i) Object id property is added and initialization with a newly generated Id when object is constructed is
    ///     provided.
    ///     (ii) Storage for reference of linked objects and get and set methods.
    /// </remarks>
    [Serializable]
    [IntroduceInterface(typeof (IDocument), OverrideAction = InterfaceOverrideAction.Ignore)]
    public class NDocumentAttribute : InstanceLevelAspect, IAspectProvider, IDocument, IScalarConstraint
    {
        /// <summary>
        ///     This method introduces several aspects:
        /// </summary>
        /// <param name="targetElement"></param>
        /// <returns></returns>
        public IEnumerable<AspectInstance> ProvideAspects(object targetElement)
        {
            var targetType = (Type) targetElement;
            yield return
                new AspectInstance(targetElement,
                    new CustomAttributeIntroductionAspect(new ObjectConstruction(typeof (NDocumentAttribute))));
        }

        /// <summary>
        ///     A custom initialization of newly created object annotated with this aspect. Used to
        ///     Initialize IRepositoryDocument members and navigable reference and collection properties.
        /// </summary>
        public override void RuntimeInitializeInstance()
        {
            Id = ObjectId.GenerateNewId();
            var navigableRequired = false;

            var accessor = ObjectAccessor.Create(Instance);
            foreach (var p in CachedTypeInfo.GetNavigableCollectionProperties(Instance.GetType()))
            {
                var attr = (NCollectionAttribute) p.GetCustomAttribute(typeof (NCollectionAttribute));
                accessor[p.Name] = attr.CreateHashSet();
                navigableRequired = true;
            }
            foreach (var p in CachedTypeInfo.GetNavigableReferenceProperties(Instance.GetType()))
            {
                var attr = (NReferenceAttribute) p.GetCustomAttribute(typeof (NReferenceAttribute));
                navigableRequired = true;
            }

            if (navigableRequired) Navigable = new Dictionary<string, object>();

            base.RuntimeInitializeInstance();
        }

        /// <summary>
        /// Implements on property change aspect for all controlled properties.
        /// </summary>
        /// <param name="args"></param>
        [OnLocationSetValueAdvice, MethodPointcut(nameof(FindControlledProperties))]
        public void OnValueChanged(LocationInterceptionArgs args)
        {
            args.ProceedSetValue();
            ControlledPropertyChanged?.Invoke(Instance, new PropertyChangedEventArgs(args.LocationName));            
        }

        /// <summary>
        ///     Gets an enumerable consisting of PropertyInfo objects for all controlled properties.
        /// </summary>
        /// <param name="target">Target type for which the controlled properties are to be enumerated.</param>
        /// <returns></returns>                                                                         
        public IEnumerable<PropertyInfo> FindControlledProperties(Type target)
        {
            return
                target.GetProperties()
                    .Where(p => p.IsDefined(typeof (NValueAttribute)) || p.IsDefined(typeof (NReferenceAttribute)));
        }

        #region IDocument

        /// <summary>
        /// Defines a unique identification of the document.
        /// </summary>
        [IntroduceMember(OverrideAction = MemberOverrideAction.OverrideOrFail)]
        public ObjectId Id { get; set; }

        /// <summary>
        ///     Handles references of navigable properties.
        /// </summary>
        [IntroduceMember(OverrideAction = MemberOverrideAction.OverrideOrFail)]
        public IDictionary<string, object> Navigable { get; set; }

        /// <summary>
        ///     Sets the value of the controlled property in the target document.
        /// </summary>
        /// <param name="propertyName">A name of the controlled property.</param>
        /// <param name="value">A value to be write to the controlled property</param>
        /// <exception cref="ArgumentOutOfRangeException">if propertyName is not valid name of the document property</exception>
        /// <exception cref="ArgumentException">if value cannot be assigned to the target property</exception>
        public void SetPropertyValue(string propertyName, object value)
        {
            var accessor = ObjectAccessor.Create(Instance);
            accessor[propertyName] = value;
        }
        /// <summary>
        ///     Gets the value of controlled property in the target document.
        /// </summary>
        /// <param name="propertyName">A name of the controlled property.</param>
        /// <returns>Value of the controlled property.</returns>
        /// <exception cref="ArgumentOutOfRangeException">if propertyName is not valid name of the document property</exception>

        public object GetPropertyValue(string propertyName)
        {
            var accessor = ObjectAccessor.Create(Instance);
            return accessor[propertyName];
        }
        /// <summary>
        ///     Occurs when a controlled property value changes.
        /// </summary>
        public event PropertyChangedEventHandler ControlledPropertyChanged;

        #endregion

        public bool ValidateConstraint(object target)
        {
            return true;
        }

        public void ValidateCode(object target)
        {
            var targetType = (Type)target;

            if (targetType.GetConstructor(new Type[] {}) == null)
            {
                Message.Write(
                    targetType, SeverityType.Error,
                    "PS0310",
                    "{0} must be non-abstract type with public parameterless constructor in order to be annotated with NDocument.",
                    targetType.Name);
            }
        }
    }
}