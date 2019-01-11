﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;

namespace Sakuno.ING.Game
{
    [DebuggerDisplay("Count = {Count}")]
    [DebuggerTypeProxy(typeof(IdTableDebuggerProxy<,,,>))]
    internal class IdTable<TId, T, TRaw, TOwner> : BindableObject, ITable<TId, T>
        where TId : struct, IComparable<TId>, IEquatable<TId>
        where T : class, IUpdatable<TId, TRaw>
        where TRaw : IIdentifiable<TId>
    {
        public event Action Updated;
        private static readonly Func<TId, TOwner, T> dummyCreation;
        private static readonly Func<TRaw, TOwner, DateTimeOffset, T> creation;

        static IdTable()
        {
            {
                var argId = Expression.Parameter(typeof(TId));
                var argOwner = Expression.Parameter(typeof(TOwner));
                var ctor = typeof(T).GetConstructor(new[] { typeof(TId), typeof(TOwner) });
                var call = Expression.New(ctor, argId, argOwner);
                dummyCreation = Expression.Lambda<Func<TId, TOwner, T>>(call, argId, argOwner).Compile();
            }
            {
                var argRaw = Expression.Parameter(typeof(TRaw));
                var argOwner = Expression.Parameter(typeof(TOwner));
                var argTime = Expression.Parameter(typeof(DateTimeOffset));
                var ctor = typeof(T).GetConstructor(new[] { typeof(TRaw), typeof(TOwner), typeof(DateTimeOffset) });
                var call = Expression.New(ctor, argRaw, argOwner, argTime);
                creation = Expression.Lambda<Func<TRaw, TOwner, DateTimeOffset, T>>(call, argRaw, argOwner, argTime).Compile();
            }
        }

        internal readonly List<T> list = new List<T>();
        private readonly TOwner owner;
        public IdTable(TOwner owner)
        {
            this.owner = owner;
            DefaultView = new BindableSnapshotCollection<T>(this, this.OrderBy(x => x.Id));
        }

        public T this[TId id]
        {
            get
            {
                if (TryGetValue(id, out var item))
                    return item;

                item = dummyCreation(id, owner);
                Add(item);
                return item;
            }
        }

        public T this[TId? id]
            => id is TId valid ? this[valid] : null;

        public IBindableCollection<T> DefaultView { get; }
        public int Count => list.Count;

        public void BatchUpdate(IEnumerable<TRaw> source, DateTimeOffset timeStamp, bool removal = true)
        {
            int i = 0;
            foreach (var raw in source)
            {
                while (i < list.Count && list[i].Id.CompareTo(raw.Id) < 0)
                    if (removal)
                        list.RemoveAt(i);
                    else
                        i++;

                if (i < list.Count && list[i].Id.Equals(raw.Id))
                    list[i++].Update(raw, timeStamp);
                else
                    list.Insert(i++, creation(raw, owner, timeStamp));
            }
            Updated?.Invoke();
            NotifyPropertyChanged(nameof(Count));
        }

        public void Add(TRaw raw, DateTimeOffset timeStamp) => Add(creation(raw, owner, timeStamp));

        public void Add(T item)
        {
            int i;
            for (i = 0; i < list.Count; i++)
            {
                if (list[i].Id.CompareTo(item.Id) > 0)
                {
                    list.Insert(i, item);
                    break;
                }
                else if (list[i].Id.Equals(item.Id))
                {
                    list[i] = item;
                    break;
                }
            }
            if (i == list.Count)
                list.Add(item);
            Updated?.Invoke();
            NotifyPropertyChanged(nameof(Count));
        }

        public T Remove(TId id)
        {
            if (TryGetValue(id, out T item))
            {
                Remove(item);
                NotifyPropertyChanged(nameof(Count));
                return item;
            }
            return null;
        }

        public bool Remove(T item)
        {
            var result = list.Remove(item);
            if (result)
            {
                Updated?.Invoke();
                NotifyPropertyChanged(nameof(Count));
            }
            return result;
        }

        public int RemoveAll(Predicate<T> predicate)
        {
            var result = list.RemoveAll(predicate);
            if (result > 0)
            {
                Updated?.Invoke();
                NotifyPropertyChanged(nameof(Count));
            }
            return result;
        }

        public void Clear()
        {
            list.Clear();
            Updated?.Invoke();
            NotifyPropertyChanged(nameof(Count));
        }

        public bool TryGetValue(TId id, out T item)
        {
            item = default;
            if (id.CompareTo(default) < 0)
                throw new ArgumentException("Negative id is not valid.");

            int lo = 0, hi = list.Count - 1;
            while (lo <= hi)
            {
                int i = lo + ((hi - lo) >> 1);
                var t = list[i];

                int order = t.Id.CompareTo(id);
                if (order == 0)
                {
                    item = t;
                    return true;
                }

                if (order < 0)
                    lo = i + 1;
                else
                    hi = i - 1;
            }

            return false;
        }

        public List<T>.Enumerator GetEnumerator() => list.GetEnumerator();
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
