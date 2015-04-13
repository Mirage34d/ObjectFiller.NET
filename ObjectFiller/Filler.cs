using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace Tynamix.ObjectFiller
{
    /// <summary>
    /// The ObjectFiller.NET fills the public properties of your .NET object
    /// with random data
    /// </summary>
    /// <typeparam name="T">Targettype of the object to fill</typeparam>
    public class Filler<T> where T : class
    {
        private readonly SetupManager _setupManager;

        /// <summary>
        /// Default constructor
        /// </summary>
        public Filler()
        {
            _setupManager = new SetupManager();
        }

        /// <summary>
        /// Call this to start the setup for the <see cref="Filler{T}"/>
        /// </summary>
        /// <returns>Fluent API setup</returns>
        public FluentFillerApi<T> Setup()
        {
            return Setup(null);
        }

        /// <summary>
        /// Call this to start the setup for the <see cref="Filler{T}"/> and use a setup which you created
        /// before with the FluentApi
        /// </summary>
        /// <param name="fillerSetupToUse">FillerSetup to use</param>
        /// <returns>Fluebt API Setup</returns>
        public FluentFillerApi<T> Setup(FillerSetup fillerSetupToUse)
        {
            if (fillerSetupToUse != null)
            {
                _setupManager.FillerSetup = fillerSetupToUse;
            }
            return new FluentFillerApi<T>(_setupManager);

        }

        /// <summary>
        /// Creates your filled object. Call this after you finished your setup with the FluentAPI and if you want
        /// to create a new object. If you want to use a existing instance use the <see cref="Fill(T)"/> method.
        /// </summary>
        public T Create()
        {
            var objectToFill = (T)CreateInstanceOfType(typeof(T).GetTypeInfo(), _setupManager.GetFor<T>(), new HashStack<TypeInfo>());

            Fill(objectToFill);

            return objectToFill;
        }

        /// <summary>
        /// Creates multiple filled objects. Call this after you finished your setup with the FluentAPI and if you want
        /// to create several new objects. If you want to use a existing instance use the <see cref="Fill(T)"/> method.
        /// </summary>
        public IEnumerable<T> Create(int count)
        {
            var typeStack = new HashStack<TypeInfo>();
            for (int n = 0; n < count; n++)
            {
                T objectToFill = (T)CreateInstanceOfType(typeof(T).GetTypeInfo(), _setupManager.GetFor<T>(), typeStack);
                Fill(objectToFill);
                yield return objectToFill;
            }
        }

        /// <summary>
        /// Fills your object instance. Call this after you finished your setup with the FluentAPI
        /// </summary>
        public T Fill(T instanceToFill)
        {
            FillInternal(instanceToFill);

            return instanceToFill;
        }


        private object CreateInstanceOfType(TypeInfo type, FillerSetupItem currentSetupItem, HashStack<TypeInfo> typeTracker)
        {
            List<object> constructorArgs = new List<object>();

            //if (type.GetConstructors().All(ctor => ctor.GetParameters().Length != 0))
            if (type.DeclaredConstructors.All(ctor => ctor.GetParameters().Length != 0))
            {
                IEnumerable<ConstructorInfo> ctorInfos;
                //if ((ctorInfos = type.GetConstructors().Where(ctr => ctr.GetParameters().Length != 0)).Count() != 0)

                if ((ctorInfos = type.DeclaredConstructors.Where(ctr => ctr.GetParameters().Length != 0)).Count() != 0)
                {
                    foreach (ConstructorInfo ctorInfo in ctorInfos.OrderBy(x => x.GetParameters().Length))
                    {
                        var paramTypes = ctorInfo.GetParameters().Select(p => p.ParameterType.GetTypeInfo()).ToArray();

                        if (paramTypes.All(t => TypeIsValidForObjectFiller(t, currentSetupItem)))
                        {
                            foreach (var paramType in paramTypes)
                            {
                                constructorArgs.Add(GetFilledObject(paramType, currentSetupItem, typeTracker));
                            }

                            break;
                        }
                    }

                    if (constructorArgs.Count == 0)
                    {
                        var message = "Could not found a constructor for type [" + type.Name + "] where the parameters can be filled with the current objectfiller setup";
                        Debug.WriteLine("ObjectFiller: " + message);
                        throw new InvalidOperationException(message);
                    }
                }
            }

            object result = Activator.CreateInstance(type.AsType(), constructorArgs.ToArray());
            return result;
        }

        public IEnumerable<PropertyInfo> GetAllProperties(TypeInfo type, List<PropertyInfo> storage = null)
        {
            if (storage == null)
                storage = new List<PropertyInfo>();

            storage.AddRange(type.DeclaredProperties);

            if (type.BaseType != typeof (object))
                return GetAllProperties(type.BaseType.GetTypeInfo(), storage);
            
            return storage;
        }

        private void FillInternal(object objectToFill, HashStack<TypeInfo> typeTracker = null)
        {
            var currentSetup = _setupManager.GetFor(objectToFill.GetType());
            var targetType = objectToFill.GetType().GetTypeInfo();

            typeTracker = typeTracker ?? new HashStack<TypeInfo>();

            if (currentSetup.TypeToRandomFunc.ContainsKey(targetType))
            {
                objectToFill = currentSetup.TypeToRandomFunc[targetType]();
                return;
            }
             
            var properties = GetAllProperties(targetType)
                             .Where(x => GetSetMethodOnDeclaringType(x) != null).ToArray();

            if (properties.Length == 0) return;

            Queue<PropertyInfo> orderedProperties = OrderPropertiers(currentSetup, properties);
            while (orderedProperties.Count != 0)
            {
                PropertyInfo property = orderedProperties.Dequeue();

                if (currentSetup.TypesToIgnore.Contains(property.PropertyType))
                {
                    continue;
                }

                if (IgnoreProperty(property, currentSetup))
                {
                    continue;
                }
                if (ContainsProperty(currentSetup.PropertyToRandomFunc.Keys, property))
                {
                    PropertyInfo p = GetPropertyFromProperties(currentSetup.PropertyToRandomFunc.Keys, property).Single();
                    SetPropertyValue(property, objectToFill, currentSetup.PropertyToRandomFunc[p]());
                    continue;
                }

                object filledObject = GetFilledObject(property.PropertyType.GetTypeInfo(), currentSetup, typeTracker);

                SetPropertyValue(property, objectToFill, filledObject);
            }
        }

        private void SetPropertyValue(PropertyInfo property, object objectToFill, object value)
        {
            if (property.CanWrite)
            {
                property.SetValue(objectToFill, value, null);
            }
            else
            {
                MethodInfo m = GetSetMethodOnDeclaringType(property);
                m.Invoke(objectToFill, new object[] { value });
            }
        }

        private Queue<PropertyInfo> OrderPropertiers(FillerSetupItem currentSetupItem, PropertyInfo[] properties)
        {
            Queue<PropertyInfo> propertyQueue = new Queue<PropertyInfo>();
            var firstProperties = currentSetupItem.PropertyOrder
                                              .Where(x => x.Value == At.TheBegin && ContainsProperty(properties, x.Key))
                                              .Select(x => x.Key).ToList();

            var lastProperties = currentSetupItem.PropertyOrder
                                              .Where(x => x.Value == At.TheEnd && ContainsProperty(properties, x.Key))
                                              .Select(x => x.Key).ToList();

            var propertiesWithoutOrder = properties.Where(x => !ContainsProperty(currentSetupItem.PropertyOrder.Keys, x)).ToList();

            //firstProperties.ForEach(propertyQueue.Enqueue);
            foreach (var firstProperty in firstProperties)
                propertyQueue.Enqueue(firstProperty);

            //propertiesWithoutOrder.ForEach(propertyQueue.Enqueue);
            foreach (var propertyInfo in propertiesWithoutOrder)
                propertyQueue.Enqueue(propertyInfo);

            foreach (var propertyInfo in lastProperties)
                propertyQueue.Enqueue(propertyInfo);

            return propertyQueue;
        }

        private bool IgnoreProperty(PropertyInfo property, FillerSetupItem currentSetupItem)
        {
            return ContainsProperty(currentSetupItem.PropertiesToIgnore, property);
        }

        private bool ContainsProperty(IEnumerable<PropertyInfo> properties, PropertyInfo property)
        {
            return GetPropertyFromProperties(properties, property).Any();
        }

        private MethodInfo GetSetMethodOnDeclaringType(PropertyInfo propInfo)
        {
            var methodInfo = propInfo.SetMethod;


            if (propInfo.DeclaringType != null)
                return methodInfo ?? propInfo
                    .DeclaringType
                    .GetRuntimeProperty(propInfo.Name)
                    .SetMethod;

            return null;
        }

        private IEnumerable<PropertyInfo> GetPropertyFromProperties(IEnumerable<PropertyInfo> properties, PropertyInfo property)
        {
            return properties.Where(x => x.Name == property.Name && x.Module.Equals(property.Module));
        }

        private object GetFilledObject(TypeInfo type, FillerSetupItem currentSetupItem, HashStack<TypeInfo> typeTracker = null)
        {
            if (HasTypeARandomFunc(type, currentSetupItem))
            {
                return GetRandomValue(type, currentSetupItem);
            }

            if (TypeIsDictionary(type))
            {
                IDictionary dictionary = GetFilledDictionary(type, currentSetupItem, typeTracker);

                return dictionary;
            }

            if (TypeIsList(type))
            {
                IList list = GetFilledList(type, currentSetupItem, typeTracker);
                return list;
            }

            if (type.IsInterface || type.IsAbstract)
            {
                return CreateInstanceOfInterfaceOrAbstractClass(type, currentSetupItem, typeTracker);
            }

            if (TypeIsPoco(type))
            {
                return GetFilledPoco(type, currentSetupItem, typeTracker);
            }

            if (TypeIsEnum(type))
            {
                return GetRandomEnumValue(type);
            }

            object newValue = GetRandomValue(type, currentSetupItem);
            return newValue;
        }

        private object GetRandomEnumValue(TypeInfo type)
        {
            // performance: Enum.GetValues() is slow due to reflection, should cache it
            Array values = Enum.GetValues(type.AsType());
            if (values.Length > 0)
            {
                int index = Random.Next() % values.Length;
                return values.GetValue(index);
            }
            return 0;
        }

        private bool CheckForCircularReference(TypeInfo targetType, HashStack<TypeInfo> typeTracker, FillerSetupItem currentSetupItem)
        {
            if (typeTracker != null)
            {
                if (typeTracker.Contains(targetType))
                {
                    if (currentSetupItem.ThrowExceptionOnCircularReference)
                    {
                        throw new InvalidOperationException(
                                string.Format(
                                    "The type {0} was already encountered before, which probably means you have a circular reference in your model. Either ignore the properties which cause this or specify explicit creation rules for them which do not rely on types.",
                                    targetType.Name));
                    }

                    return true;
                }
            }

            return false;
        }

        private object GetFilledPoco(TypeInfo type, FillerSetupItem currentSetupItem, HashStack<TypeInfo> typeTracker)
        {
            if (CheckForCircularReference(type, typeTracker, currentSetupItem))
            {
                return GetDefaultValueOfType(type);
            }
            typeTracker.Push(type);

            object result = CreateInstanceOfType(type, currentSetupItem, typeTracker);

            FillInternal(result, typeTracker);

            if (typeTracker != null)
            {
                // once we fully filled the object, we can pop so other properties in the hierarchy can use the same types
                typeTracker.Pop();
            }

            return result;
        }

        private IDictionary GetFilledDictionary(TypeInfo propertyType, FillerSetupItem currentSetupItem, HashStack<TypeInfo> typeTracker)
        {
            IDictionary dictionary = (IDictionary)Activator.CreateInstance(propertyType.AsType());
            var keyType = propertyType.GenericTypeArguments[0].GetTypeInfo();
            var valueType = propertyType.GenericTypeArguments[1].GetTypeInfo();

            int maxDictionaryItems = Random.Next(currentSetupItem.DictionaryKeyMinCount,
                currentSetupItem.DictionaryKeyMaxCount);
            for (int i = 0; i < maxDictionaryItems; i++)
            {
                object keyObject = GetFilledObject(keyType, currentSetupItem, typeTracker);

                if (dictionary.Contains(keyObject))
                {
                    string message = string.Format("Generating Keyvalue failed because it generates always the same data for type [{0}]. Please check your setup.", keyType);
                    Debug.WriteLine("ObjectFiller: " + message);
                    throw new ArgumentException(message);
                }

                object valueObject = GetFilledObject(valueType, currentSetupItem, typeTracker);
                dictionary.Add(keyObject, valueObject);
            }
            return dictionary;
        }

        private static bool HasTypeARandomFunc(TypeInfo type, FillerSetupItem currentSetupItem)
        {
            return currentSetupItem.TypeToRandomFunc.ContainsKey(type);
        }


        private IList GetFilledList(TypeInfo propertyType, FillerSetupItem currentSetupItem, HashStack<TypeInfo> typeTracker)
        {
            var genType = propertyType.GenericTypeArguments[0].GetTypeInfo();

            if (CheckForCircularReference(genType, typeTracker, currentSetupItem))
            {
                return null;
            }

            IList list;
            if (!propertyType.IsInterface && propertyType.ImplementedInterfaces.Any(x => x.GetGenericTypeDefinition() == typeof(ICollection<>)))
            {
                list = (IList)Activator.CreateInstance(propertyType.AsType());
            }
            else if (propertyType.IsGenericType
                && propertyType.GetGenericTypeDefinition() == typeof(IEnumerable<>)
                   || propertyType.ImplementedInterfaces.Any(x => x.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
            {
                Type openListType = typeof(List<>);
                Type genericListType = openListType.MakeGenericType(genType.AsType());
                list = (IList)Activator.CreateInstance(genericListType);
            }
            else
            {
                list = (IList)Activator.CreateInstance(propertyType.AsType());
            }


            int maxListItems = Random.Next(currentSetupItem.ListMinCount, currentSetupItem.ListMaxCount);
            for (int i = 0; i < maxListItems; i++)
            {
                object listObject = GetFilledObject(genType, currentSetupItem, typeTracker);
                list.Add(listObject);
            }
            return list;
        }

        private object CreateInstanceOfInterfaceOrAbstractClass(TypeInfo interfaceType, FillerSetupItem setupItem, HashStack<TypeInfo> typeTracker)
        {
            object result;
            if (setupItem.TypeToRandomFunc.ContainsKey(interfaceType))
            {
                return setupItem.TypeToRandomFunc[interfaceType]();
            }
            if (setupItem.InterfaceToImplementation.ContainsKey(interfaceType))
            {
                TypeInfo implType = setupItem.InterfaceToImplementation[interfaceType];
                result = CreateInstanceOfType(implType, setupItem, typeTracker);
            }
            else
            {
                if (setupItem.InterfaceMocker == null)
                {
                    string message = string.Format("ObjectFiller Interface mocker missing and type [{0}] not registered", interfaceType.Name);
                    Debug.WriteLine("ObjectFiller: " + message);
                    throw new InvalidOperationException(message);
                }

                MethodInfo method = setupItem.InterfaceMocker.GetType().GetTypeInfo().GetDeclaredMethod("Create");
                MethodInfo genericMethod = method.MakeGenericMethod(new[] { interfaceType.AsType() });
                result = genericMethod.Invoke(setupItem.InterfaceMocker, null);
            }
            FillInternal(result, typeTracker);
            return result;
        }

        private object GetRandomValue(TypeInfo propertyType, FillerSetupItem setupItem)
        {
            if (setupItem.TypeToRandomFunc.ContainsKey(propertyType))
            {
                return setupItem.TypeToRandomFunc[propertyType]();
            }

            if (setupItem.IgnoreAllUnknownTypes)
            {
                return GetDefaultValueOfType(propertyType);
            }

            string message = "The type [" + propertyType.Name + "] was not registered in the randomizer.";
            Debug.WriteLine("ObjectFiller: " + message);
            throw new TypeInitializationException(propertyType.FullName, new Exception(message));
        }

        private static object GetDefaultValueOfType(TypeInfo propertyType)
        {
            if (propertyType.IsValueType)
            {
                return Activator.CreateInstance(propertyType.AsType());
            }
            return null;
        }

        private static bool TypeIsValidForObjectFiller(TypeInfo type, FillerSetupItem currentSetupItem)
        {
            return HasTypeARandomFunc(type, currentSetupItem)
                   || (TypeIsList(type) && ListParamTypeIsValid(type, currentSetupItem))
                   || (TypeIsDictionary(type) && DictionaryParamTypesAreValid(type, currentSetupItem))
                   || TypeIsPoco(type)
                   || (type.IsInterface
                        && currentSetupItem.InterfaceToImplementation.ContainsKey(type)
                        || currentSetupItem.InterfaceMocker != null);

        }

        private static bool DictionaryParamTypesAreValid(TypeInfo type, FillerSetupItem currentSetupItem)
        {
            if (!TypeIsDictionary(type))
            {
                return false;
            }

            var keyType = type.GenericTypeArguments[0].GetTypeInfo();
            var valueType = type.GenericTypeArguments[1].GetTypeInfo();

            return TypeIsValidForObjectFiller(keyType, currentSetupItem) &&
                   TypeIsValidForObjectFiller(valueType, currentSetupItem);
        }

        private static bool ListParamTypeIsValid(TypeInfo type, FillerSetupItem setupItem)
        {
            if (!TypeIsList(type))
            {
                return false;
            }
            var genType = type.GenericTypeArguments[0].GetTypeInfo();

            return TypeIsValidForObjectFiller(genType, setupItem);
        }

        private static bool TypeIsPoco(TypeInfo type)
        {
            return !type.IsValueType
                   && !type.IsArray
                   && type.IsClass
                   && type.DeclaredProperties.Any()
                   && (type.Namespace == null
                       || (!type.Namespace.StartsWith("System")
                           && !type.Namespace.StartsWith("Microsoft")));
        }

        private static bool TypeIsDictionary(TypeInfo type)
        {
            return type.ImplementedInterfaces.Any(x => x == typeof(IDictionary));
        }

        private static bool TypeIsList(TypeInfo type)
        {
            return !type.IsArray
                      && type.IsGenericType
                      && type.GenericTypeArguments.Length != 0
                      && (type.GetGenericTypeDefinition() == typeof(IEnumerable<>)
                        || type.ImplementedInterfaces.Any(x => x == typeof(IEnumerable)));
        }

        private static bool TypeIsEnum(TypeInfo type)
        {
            return type.IsEnum;
        }
    }
}
