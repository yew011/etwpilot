/* 
Licensed to the Apache Software Foundation (ASF) under one
or more contributor license agreements.  See the NOTICE file
distributed with this work for additional information
regarding copyright ownership.  The ASF licenses this file
to you under the Apache License, Version 2.0 (the
"License"); you may not use this file except in compliance
with the License.  You may obtain a copy of the License at

  http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing,
software distributed under the License is distributed on an
"AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
KIND, either express or implied.  See the License for the
specific language governing permissions and limitations
under the License.
*/
using System.Reflection;

namespace EtwPilot.Utilities
{
    public static class ReflectionHelper
    {
        public static List<string>? GetPropertyNamesByAttribute<T>()
        {
            try
            {
                var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
                if (properties.Count() > 0)
                {
                    var props = new List<string>();
                    foreach (var prop in properties)
                    {
                        if (prop.CustomAttributes.Count() != 1)
                        {
                            continue;
                        }
                        if (prop.CustomAttributes.ElementAt(0).AttributeType == typeof(T))
                        {
                            props.Add(prop.Name);
                        }
                    }
                    return props;
                }
            }
            catch (Exception) { }
            return null;
        }

        public static dynamic? GetPropertyValue(object Object, string PropertyName)
        {
            object? value = null;
            try
            {
                var properties = Object.GetType().GetProperties(
                    BindingFlags.Public | BindingFlags.Instance);
                properties.ToList().ForEach(property =>
                {
                    if (property.Name.ToLower() == PropertyName.ToLower())
                    {
                        value = property.GetValue(Object, null);
                    }
                });
            }
            catch (Exception) { }

            return value;
        }
    }
}
