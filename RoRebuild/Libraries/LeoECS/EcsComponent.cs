// ----------------------------------------------------------------------------
// The MIT License
// Simple Entity Component System framework https://github.com/Leopotam/ecs
// Copyright (c) 2017-2020 Leopotam <leopotam@gmail.com>
// ----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

// ReSharper disable ClassNeverInstantiated.Global

namespace Leopotam.Ecs {
    /// <summary>
    /// Marks component type to be not auto-filled as GetX in filter.
    /// </summary>
    public interface IEcsIgnoreInFilter { }

    /// <summary>
    /// Marks component type to be auto removed from world.
    /// </summary>
    [Obsolete ("Use EcsSystems.OneFrame() for register one-frame components and Run() for processing and cleanup.")]
    public interface IEcsOneFrame { }

    /// <summary>
    /// Marks component type as resettable with custom logic.
    /// </summary>
    public interface IEcsAutoReset {
        void Reset ();
    }

    /// <summary>
    /// Marks field of IEcsSystem class to be ignored during dependency injection.
    /// </summary>
    public sealed class EcsIgnoreInjectAttribute : Attribute { }

    /// <summary>
    /// Marks field of component to be not checked for null on component removing.
    /// Works only in DEBUG mode!
    /// </summary>
    [System.Diagnostics.Conditional ("DEBUG")]
    [AttributeUsage (AttributeTargets.Field)]
    public sealed class EcsIgnoreNullCheckAttribute : Attribute { }

    /// <summary>
    /// Global descriptor of used component type.
    /// </summary>
    /// <typeparam name="T">Component type.</typeparam>
    public static class EcsComponentType<T> where T : class {
        // ReSharper disable StaticMemberInGenericType
        public static readonly int TypeIndex;
        public static readonly Type Type;
        public static readonly bool IsAutoReset;
        public static readonly bool IsIgnoreInFilter;
        [Obsolete ("Use EcsSystems.OneFrame() for register one-frame components and Run() for processing and cleanup.")]
        public static readonly bool IsOneFrame;
        // ReSharper restore StaticMemberInGenericType

        static EcsComponentType () {
            TypeIndex = Interlocked.Increment (ref EcsComponentPool.ComponentTypesCount);
            Type = typeof (T);
            IsAutoReset = typeof (IEcsAutoReset).IsAssignableFrom (Type);
            IsIgnoreInFilter = typeof (IEcsIgnoreInFilter).IsAssignableFrom (Type);
#pragma warning disable 618
            IsOneFrame = typeof (IEcsOneFrame).IsAssignableFrom (Type);
#pragma warning restore 618
        }
    }

#if ENABLE_IL2CPP
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption (Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption (Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
#endif
    public sealed class EcsComponentPool {
        /// <summary>
        /// Global component type counter.
        /// First component will be "1" for correct filters updating (add component on positive and remove on negative).
        /// </summary>
        internal static int ComponentTypesCount;

#if DEBUG
        readonly List<System.Reflection.FieldInfo> _nullableFields = new List<System.Reflection.FieldInfo> (8);
#endif

        public object[] Items = new Object[128];

        Func<object> _customCtor;
        readonly Type _type;
        readonly bool _isAutoReset;

        int[] _reservedItems = new int[128];
        int _itemsCount;
        int _reservedItemsCount;

        internal EcsComponentPool (Type cType, bool isAutoReset) {
            _type = cType;
            _isAutoReset = isAutoReset;
#if DEBUG
            // collect all marshal-by-reference fields.
            var fields = _type.GetFields ();
            for (var i = 0; i < fields.Length; i++) {
                var field = fields[i];
                if (!Attribute.IsDefined (field, typeof (EcsIgnoreNullCheckAttribute))) {
                    var type = field.FieldType;
                    var underlyingType = Nullable.GetUnderlyingType (type);
                    if (!type.IsValueType || (underlyingType != null && !underlyingType.IsValueType)) {
                        if (type != typeof (string)) {
                            _nullableFields.Add (field);
                        }
                    }
                    if (type == typeof (EcsEntity)) {
                        _nullableFields.Add (field);
                    }
                }
            }
#endif
        }

        /// <summary>
        /// Sets custom constructor for component instances.
        /// </summary>
        /// <param name="ctor"></param>
        public void SetCustomCtor (Func<object> ctor) {
#if DEBUG
            // ReSharper disable once JoinNullCheckWithUsage
            if (ctor == null) { throw new Exception ("Ctor is null."); }
#endif
            _customCtor = ctor;
        }

        /// <summary>
        /// Sets new capacity (if more than current amount).
        /// </summary>
        /// <param name="capacity">New value.</param>
        public void SetCapacity (int capacity) {
            if (capacity > Items.Length) {
                Array.Resize (ref Items, capacity);
            }
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public int New () {
            int id;
            if (_reservedItemsCount > 0) {
                id = _reservedItems[--_reservedItemsCount];
            } else {
                id = _itemsCount;
                if (_itemsCount == Items.Length) {
                    Array.Resize (ref Items, _itemsCount << 1);
                }
                var instance = _customCtor != null ? _customCtor () : Activator.CreateInstance (_type);
                // reset brand new instance if component implements IEcsAutoReset.
                if (_isAutoReset) {
                    ((IEcsAutoReset) instance).Reset ();
                }
                Items[_itemsCount++] = instance;
            }
            return id;
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public object GetItem (int idx) {
            return Items[idx];
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public void Recycle (int idx) {
            if (_isAutoReset) {
                ((IEcsAutoReset) Items[idx]).Reset ();
            }
#if DEBUG
            // check all marshal-by-reference typed fields for nulls.
            var obj = Items[idx];
            for (int i = 0, iMax = _nullableFields.Count; i < iMax; i++) {
                if (_nullableFields[i].FieldType.IsValueType) {
                    if (_nullableFields[i].FieldType == typeof (EcsEntity) && ((EcsEntity) _nullableFields[i].GetValue (obj)).Owner != null) {
                        throw new Exception (
                            $"Memory leak for \"{_type.Name}\" component: \"{_nullableFields[i].Name}\" field not null-ed with EcsEntity.Null. If you are sure that it's not - mark field with [EcsIgnoreNullCheck] attribute.");
                    }
                } else {
                    if (_nullableFields[i].GetValue (obj) != null) {
                        throw new Exception (
                            $"Memory leak for \"{_type.Name}\" component: \"{_nullableFields[i].Name}\" field not null-ed. If you are sure that it's not - mark field with [EcsIgnoreNullCheck] attribute.");
                    }
                }
            }
#endif
            if (_reservedItemsCount == _reservedItems.Length) {
                Array.Resize (ref _reservedItems, _reservedItemsCount << 1);
            }
            _reservedItems[_reservedItemsCount++] = idx;
        }
    }
}