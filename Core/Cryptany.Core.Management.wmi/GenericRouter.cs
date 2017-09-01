/*
   Copyright 2006-2017 Cryptany, Inc.

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/
using System;
using System.Collections.Generic;
using System.Text;
using System.Management.Instrumentation;


namespace Cryptany.Core.Management.WMI
{
    [InstrumentationClass(InstrumentationType.Instance)]
    public class GenericRouter
    {
        public int Index;
        /// <summary>
        /// Queue name  
        /// </summary>
        public string QueueName
        {
            get
            {
                return "cryptany_routerinputqueue" + Index;
            }
        }

        public GenericRouter()
        {
            Index = 0;
            Instrumentation.Publish(this);
        }
    }
}
