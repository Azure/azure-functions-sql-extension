// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace SqlExtension.Tests
{
    public class TestData
    {
        public int ID { get; set; }

        public string Name { get; set; }

        public double Cost { get; set; }

        public DateTime Timestamp { get; set; }

        public override bool Equals(object obj)
        {
            if (!(obj is TestData otherData))
            {
                return false;
            }
            return ID == otherData.ID && Cost == otherData.Cost && ((Name == null && otherData.Name == null) ||
                string.Equals(Name, otherData.Name, StringComparison.OrdinalIgnoreCase)) && Timestamp.Equals(otherData.Timestamp);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ID, Name, Cost, Timestamp);
        }
    }
}
