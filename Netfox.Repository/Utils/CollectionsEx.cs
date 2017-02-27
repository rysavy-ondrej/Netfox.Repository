using System;
using System.Collections.Generic;

namespace Netfox.Repository.Utils
{
    /// <summary>
    /// This static class implements some extensions to collection classes.
    /// </summary>
    public static class CollectionsEx
    {
        /// <summary>
        ///     Slices a sequence into a sub-sequences each containing maxItemsPerSlice, except for the last
        ///     which will contain any items left over
        /// </summary>
        public static IEnumerable<IEnumerable<T>> Slice<T>(this IEnumerable<T> sequence, int maxItemsPerSlice)
        {
            if (maxItemsPerSlice <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxItemsPerSlice),
                    "maxItemsPerSlice must be greater than 0");
            }

            var slice = new List<T>(maxItemsPerSlice);

            foreach (var item in sequence)
            {
                slice.Add(item);

                if (slice.Count == maxItemsPerSlice)
                {
                    yield return slice.ToArray();
                    slice.Clear();
                }
            }

            // return the "crumbs" that 
            // didn't make it into a full slice
            if (slice.Count > 0)
            {
                yield return slice.ToArray();
            }
        }
    }
}