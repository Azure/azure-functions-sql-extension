// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Extensions.Sql.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Unit
{
    public class IEnumerableExtensionsTests
    {
        public static readonly TheoryData<int[], int> BatchData = new()
        {
            { new int[] { 1, 2, 3, 4, 5 }, 1 }, // One by one
            { new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 3 }, // Bigger non-single batch
            { new int[] { 1, 2, 3, 4, 5 } , 5 }, // All one batch
            { new int[] { 1 }, 2 }, // Batch size greater than array
        };

        [Theory]
        [MemberData(nameof(BatchData))]
        public void Batch(IEnumerable<int> array, int batchSize)
        {
            int totalCount = 0;
            foreach (IEnumerable<int> batch in array.Batch(batchSize))
            {
                int batchCount = batch.Count();
                totalCount += batchCount;
                Assert.True(batch.Count() <= batchSize);
            }
            Assert.Equal(totalCount, array.Count());
        }

        [Fact]
        public void Batch_Invalid()
        {
            // Array must be non-null
            Assert.ThrowsAny<Exception>(() => IEnumerableExtensions.Batch<int>(null, 0).Count());

            // Size must be >= 1
            Assert.ThrowsAny<Exception>(() => IEnumerableExtensions.Batch(new int[] { 1, 2, 3 }, 0).Count());
            Assert.ThrowsAny<Exception>(() => IEnumerableExtensions.Batch(new int[] { 1, 2, 3 }, -1).Count());
        }

        public static readonly TheoryData<int[], int, int[]> TakeLastData = new()
        {
            { new int[] { 1, 2, 3, 4, 5 }, 1 , new int[] { 5 } }, // Take only last number
            { new int[] { 1, 2, 3, 4, 5 }, 3 , new int[] { 3, 4, 5 } }, // Take some middle set of numbers
            { new int[] { 1, 2, 3, 4, 5 }, 6 , new int[] { 1, 2, 3, 4, 5 } }, // Take more than exists in array
            { new int[] { 1, 2, 3, 4, 5 }, 0 , Array.Empty<int>() }, // No numbers
            { new int[] { 1, 2, 3, 4, 5 }, 0 , Array.Empty<int>() }, // Negative numbers (returns empty)
        };

        [Theory]
        [MemberData(nameof(TakeLastData))]
        public void TakeLast(IEnumerable<int> array, int takeCount, IEnumerable<int> expectedValues)
        {
            IEnumerable<int> taken = IEnumerableExtensions.TakeLast(array, takeCount);
            Assert.Equal(taken, expectedValues);
        }

        [Fact]
        public void TakeLast_Invalid()
        {
            // IEnumerable must be non-null
            Assert.ThrowsAny<Exception>(() => { IEnumerableExtensions.TakeLast<int>(null, 0); });
        }
    }
}
