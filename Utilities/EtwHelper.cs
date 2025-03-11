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
using System.Diagnostics;

namespace EtwPilot.Utilities
{
    public static class EtwHelper
    {
        public static string GetEtwStringList(List<string> Values)
        {
            var exes = string.Join(';', Values);
            var length = (exes.Length + 1) * 2;
            if (length > 1024)
            {
                //
                // ETW filtering allows a maximum length of 1024 bytes for these parameters
                // This should already have been validated by the form validation.
                //
                Debug.Assert(false);
            }
            return exes;
        }
    }
}
