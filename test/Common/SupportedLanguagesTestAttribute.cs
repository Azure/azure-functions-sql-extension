// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using Xunit.Sdk;
using System.Reflection;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common
{
    /// <summary>
    /// Base attribute class for adding language parameter data to tests. This allows
    /// tests to be run against all the supported languages list that are specified in the SupportedLanguages
    /// list of this class (ex: CSharp, JavaScript)
    /// </summary>
    public abstract class LanguageTestAttribute : DataAttribute
    {
        // add all the list of supported languages.
        protected enum SupportedLanguages
        {
            CSharp
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
        /// Load supported languages as test parameters data for the test.
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
        /// Load the test parameters data for each language for inline use of the test.
        /// </summary>
        /// <param name="args">The test parameters to insert inline for the test</param>
        public SupportedLanguagesAttribute(params object[] args)
        {
            var langData = new List<List<object>>();
            var listOfValues = new List<object>();

            foreach (string lang in Enum.GetNames(typeof(SupportedLanguages)))
            {
                foreach (object val in args)
                {
                    listOfValues.Add(val);
                }
                listOfValues.Add(lang);
                langData.Add(listOfValues);
            }
            this.testData.Add(this._propertyName, langData);
        }
    }

    public class UnsupportedLanguagesAttribute : LanguageTestAttribute
    {
        /// <summary>
        /// Load only supported languages excluding the ones from the provided parameters.
        /// </summary>
        public UnsupportedLanguagesAttribute(params object[] argsWithUnsupportedLangs)
        {
            var langData = new List<List<object>>();
            var listOfValues = new List<object>();

            // get the unsupported list of languages from the argsWithUnsupportedLangs
            string[] argsAsString = Array.ConvertAll(argsWithUnsupportedLangs, x => x.ToString());
            IEnumerable<string> unsupportedList = argsAsString.Intersect(Enum.GetNames(typeof(SupportedLanguages)));
            // get the actual parameters filtering out the unsupported language list.
            object[] args = Array.FindAll(argsWithUnsupportedLangs, x => !unsupportedList.Contains(x));
            foreach (string lang in Enum.GetNames(typeof(SupportedLanguages)))
            {
                if (!unsupportedList.Contains(lang))
                {
                    foreach (object val in args)
                    {
                        listOfValues.Add(val);
                    }
                    listOfValues.Add(lang);
                    langData.Add(listOfValues);
                }
            }
            this.testData.Add(this._propertyName, langData);
        }
    }
}