using System;
using System.Collections.Generic;
using System.Reflection;

namespace Tynamix.ObjectFiller
{
    internal class FillerSetupItem
    {
        internal FillerSetupItem()
        {
            ListMinCount = 1;
            ListMaxCount = 25;
            DictionaryKeyMinCount = 1;
            DictionaryKeyMaxCount = 10;
            TypeToRandomFunc = new Dictionary<TypeInfo, Func<object>>();
            PropertyToRandomFunc = new Dictionary<PropertyInfo, Func<object>>();
            PropertiesToIgnore = new List<PropertyInfo>();
            PropertyOrder = new Dictionary<PropertyInfo, At>();
            TypesToIgnore = new List<Type>();
            InterfaceToImplementation = new Dictionary<TypeInfo, TypeInfo>();
            IgnoreAllUnknownTypes = false;

            SetDefaultRandomizer();
        }

        private void SetDefaultRandomizer()
        {
            var mnemonic = new MnemonicString(20);
            var doublePlugin = new DoubleRange();
            var dateTimeRandomizer = new DateTimeRange(new System.DateTime(1970, 1, 1));
            TypeToRandomFunc[typeof(string).GetTypeInfo()] = mnemonic.GetValue;
            TypeToRandomFunc[typeof(bool).GetTypeInfo()] = () => Random.Next(0, 2) == 1;
            TypeToRandomFunc[typeof(bool?).GetTypeInfo()] = () => new RandomListItem<bool?>(true, false, null).GetValue();
            TypeToRandomFunc[typeof(short).GetTypeInfo()] = () => (short)Random.Next(-32767, 32767);
            TypeToRandomFunc[typeof(short?).GetTypeInfo()] = () => (short)Random.Next(-32767, 32767);
            TypeToRandomFunc[typeof(int).GetTypeInfo()] = () => Random.Next();
            TypeToRandomFunc[typeof(int?).GetTypeInfo()] = () => Random.Next();
            TypeToRandomFunc[typeof(long).GetTypeInfo()] = () => (long)Random.Next();
            TypeToRandomFunc[typeof(long?).GetTypeInfo()] = () => (long)Random.Next();
            TypeToRandomFunc[typeof(float).GetTypeInfo()] = () => (float)doublePlugin.GetValue();
            TypeToRandomFunc[typeof(float?).GetTypeInfo()] = () => (float?)doublePlugin.GetValue();
            TypeToRandomFunc[typeof(double).GetTypeInfo()] = () => doublePlugin.GetValue();
            TypeToRandomFunc[typeof(double?).GetTypeInfo()] = () => doublePlugin.GetValue();
            TypeToRandomFunc[typeof(decimal).GetTypeInfo()] = () => (decimal)Random.Next();
            TypeToRandomFunc[typeof(decimal?).GetTypeInfo()] = () => (decimal)Random.Next();
            TypeToRandomFunc[typeof(Guid).GetTypeInfo()] = () => Guid.NewGuid();
            TypeToRandomFunc[typeof(Guid?).GetTypeInfo()] = () => Guid.NewGuid();
            TypeToRandomFunc[typeof(System.DateTime).GetTypeInfo()] = () => dateTimeRandomizer.GetValue();
            TypeToRandomFunc[typeof(System.DateTime?).GetTypeInfo()] = () => dateTimeRandomizer.GetValue();
            TypeToRandomFunc[typeof(byte).GetTypeInfo()] = () => (byte)Random.Next();
            TypeToRandomFunc[typeof(byte?).GetTypeInfo()] = () => (byte?)Random.Next();
            TypeToRandomFunc[typeof(char).GetTypeInfo()] = () => (char)Random.Next();
            TypeToRandomFunc[typeof(char?).GetTypeInfo()] = () => (char)Random.Next();
            TypeToRandomFunc[typeof(ushort).GetTypeInfo()] = () => (ushort)Random.Next();
            TypeToRandomFunc[typeof(ushort?).GetTypeInfo()] = () => (ushort)Random.Next();
            TypeToRandomFunc[typeof(uint).GetTypeInfo()] = () => (uint)Random.Next();
            TypeToRandomFunc[typeof(uint?).GetTypeInfo()] = () => (uint)Random.Next();
            TypeToRandomFunc[typeof(ulong).GetTypeInfo()] = () => (ulong)Random.Next();
            TypeToRandomFunc[typeof(ulong?).GetTypeInfo()] = () => (ulong)Random.Next();
            TypeToRandomFunc[typeof(IntPtr).GetTypeInfo()] = () => default(IntPtr);
            TypeToRandomFunc[typeof(IntPtr?).GetTypeInfo()] = () => default(IntPtr);
            TypeToRandomFunc[typeof(TimeSpan).GetTypeInfo()] = () => new TimeSpan(Random.Next());
            TypeToRandomFunc[typeof(TimeSpan?).GetTypeInfo()] = () => new TimeSpan(Random.Next());
        }

        /// <summary>
        /// Defines in which order the properties get handled.
        /// </summary>
        internal Dictionary<PropertyInfo, At> PropertyOrder { get; private set; }

        /// <summary>
        /// Contains the Type to random data generator func
        /// </summary>
        internal Dictionary<TypeInfo, Func<object>> TypeToRandomFunc { get; private set; }

        /// <summary>
        /// Contains the Property to random data generator func
        /// </summary>
        internal Dictionary<PropertyInfo, Func<object>> PropertyToRandomFunc { get; private set; }

        /// <summary>
        /// Contains the type of interface with the corresponding implementation
        /// </summary>
        internal Dictionary<TypeInfo, TypeInfo> InterfaceToImplementation { get; private set; }

        /// <summary>
        /// List with all properties which will be ignored while generating test data
        /// </summary>
        internal List<PropertyInfo> PropertiesToIgnore { get; private set; }

        /// <summary>
        /// All types which will be ignored completly
        /// </summary>
        internal List<Type> TypesToIgnore { get; private set; }

        /// <summary>
        /// Minimum count of list items which will be generated 
        /// </summary>
        internal int ListMinCount { get; set; }

        /// <summary>
        /// Maximum count of list items which will be generated 
        /// </summary>
        internal int ListMaxCount { get; set; }

        /// <summary>
        /// Minimum count of key items within a dictionary which will be generated 
        /// </summary>
        public int DictionaryKeyMinCount { get; set; }

        /// <summary>
        /// Maximum count of key items within a dictionary which will be generated 
        /// </summary>
        internal int DictionaryKeyMaxCount { get; set; }

        /// <summary>
        /// Interface Mocker for interface generation
        /// </summary>
        internal IInterfaceMocker InterfaceMocker { get; set; }

        /// <summary>
        /// True if all unknown types will be ignored by the objectfiller
        /// </summary>
        internal bool IgnoreAllUnknownTypes { get; set; }

        /// <summary>
        /// True if an exception will be thrown if an circular reference occured
        /// </summary>
        public bool ThrowExceptionOnCircularReference { get; set; }

    }
}
