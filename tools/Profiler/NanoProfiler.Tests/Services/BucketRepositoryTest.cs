// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Linq;
using nanoFramework.Tools.NanoProfiler.Services;

namespace NanoProfiler.Tests.Services
{
    [TestClass]
    public class BucketRepositoryTest
    {
        private readonly BucketProvider _bucketProvider;
        public BucketRepositoryTest()
        {
            _bucketProvider =  new BucketProvider();
        }

        [TestMethod]
        public void GetBucketsByScaleFactor_WintAnyValue_ReturnCorrectly()
        {
            var result = _bucketProvider.GetBucketsByScaleFactor(2.0);
            Assert.AreEqual(result.Count(), 27);
            Assert.AreEqual(result[20].maxSize, 16777215);
            
            var result2 = _bucketProvider.GetBucketsByScaleFactor(2.0);
            Assert.AreEqual(result2.Count(), 27);

        }
    }
}
