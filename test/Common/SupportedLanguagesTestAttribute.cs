// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using Xunit.Sdk;
using System.Reflection;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common
{
    public abstract class LanguageTestAttribute : DataAttribute
    {
        protected enum SupportedLanguages
        {
            CSharp
            /*JavaScript,
            Python,
             TypeScript */
        }
        protected readonly Dictionary<string, List<List<object>>> testData = new Dictionary<string, List<List<object>>>();
        protected readonly string _propertyName = "data";

        /// <inheritDoc />
        public override IEnumerable<object[]> GetData(MethodInfo testMethod)
        {
            if (testMethod == null) { throw new ArgumentNullException(nameof(testMethod)); }

            if (this.testData.Count == 0) { throw new EmptyException(nameof(testMethod)); }
            string serializedData = JsonConvert.SerializeObject(this.testData);
            var allData = JObject.Parse(serializedData);
            JToken data = allData[this._propertyName];
            return data.ToObject<List<object[]>>();
        }

    }
    public class SupportedLanguagesAttribute : LanguageTestAttribute
    {
        /// <summary>
        /// Load supported languages for inline data for a theory
        /// </summary>
        public SupportedLanguagesAttribute()
        {
            var langData = new List<List<object>>();
            foreach (string lang in Enum.GetNames(typeof(SupportedLanguages)))
            {
                var listOfValues = new List<object>(1) { lang };
                langData.Add(listOfValues);
            }
            this.testData.Add(this._propertyName, langData);
        }
        /// <summary>
        /// Load data from a inline data for a theory
        /// </summary>
        /// <param name="n">The number of products to insert for the test</param>
        /// <param name="cost">The cost of the products for the test</param>
        public SupportedLanguagesAttribute(int n, int cost)
        {
            var langData = new List<List<object>>();
            foreach (string lang in Enum.GetNames(typeof(SupportedLanguages)))
            {
                var listOfValues = new List<object>(3) { n, cost, lang };
                langData.Add(listOfValues);
            }
            this.testData.Add(this._propertyName, langData);
        }

        /// <summary>
        /// Load data from a inline data for a theory
        /// </summary>
        /// <param name="id">The id of products for the test</param>
        /// <param name="name">The name of products for the test</param>
        /// <param name="cost">The cost of the product for the test</param>
        public SupportedLanguagesAttribute(int id, string name, int cost)
        {
            var langData = new List<List<object>>();
            foreach (string lang in Enum.GetNames(typeof(SupportedLanguages)))
            {
                var listOfValues = new List<object>(4) { id, name, cost, lang };
                langData.Add(listOfValues);
            }
            this.testData.Add(this._propertyName, langData);
        }
    }

    public class UnsupportedLanguagesAttribute : LanguageTestAttribute
    {
        /// <summary>
        /// Load supported languages for inline data for a theory
        /// </summary>
        public UnsupportedLanguagesAttribute(string[] args)
        {
            var langData = new List<List<object>>();
            foreach (string lang in Enum.GetNames(typeof(SupportedLanguages)))
            {
                if (Array.IndexOf(args, lang) == -1)
                {
                    langData.Add(new List<object>(1) { lang });
                }
            }
            this.testData.Add(this._propertyName, langData);
        }
    }
}