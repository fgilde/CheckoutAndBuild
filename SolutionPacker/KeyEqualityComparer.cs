using System;
using System.Collections.Generic;

namespace SolutionPacker
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <seealso cref="System.Collections.Generic.EqualityComparer{T}" />
    internal class KeyEqualityComparer<T> : EqualityComparer<T>
    {
        private readonly Func<T, object> keyFunc;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Collections.Generic.EqualityComparer`1"/> class.
        /// </summary>
        public KeyEqualityComparer(Func<T, object> keyFunc)
        {
            this.keyFunc = keyFunc;
        }

        /// <summary>
        /// When overridden in a derived class, determines whether two objects of type <paramref name="T"/> are equal.
        /// </summary>
        /// <returns>
        /// true if the specified objects are equal; otherwise, false.
        /// </returns>
        /// <param name="x">The first object to compare.</param><param name="y">The second object to compare.</param>
        public override bool Equals(T x, T y)
        {
            return Equals(keyFunc(x), keyFunc(y));
        }

        /// <summary>
        /// When overridden in a derived class, serves as a hash function for the specified object for hashing algorithms and data structures, such as a hash table.
        /// </summary>
        /// <returns>
        /// A hash code for the specified object.
        /// </returns>
        /// <param name="obj">The object for which to get a hash code.</param><exception cref="T:System.ArgumentNullException">The type of <paramref name="obj"/> is a reference type and <paramref name="obj"/> is null.</exception>
        public override int GetHashCode(T obj)
        {
            return obj == null ? 0 : keyFunc(obj)?.GetHashCode() ?? 0;
        }
    }
}