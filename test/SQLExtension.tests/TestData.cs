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
            var otherData = obj as TestData;
            if (otherData == null)
            {
                return false;
            }
            return this.ID == otherData.ID && this.Cost == otherData.Cost && ((this.Name == null && otherData.Name == null) ||
                string.Equals(this.Name, otherData.Name, StringComparison.OrdinalIgnoreCase)) && this.Timestamp.Equals(otherData.Timestamp);
        }
    }
}
