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
            return this.ID == otherData.ID && this.Cost == otherData.Cost && ((this.Name == null && otherData.Name == null) ||
                string.Equals(this.Name, otherData.Name, StringComparison.OrdinalIgnoreCase)) && this.Timestamp.Equals(otherData.Timestamp);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ID, Name, Cost, Timestamp);
        }
    }
}
