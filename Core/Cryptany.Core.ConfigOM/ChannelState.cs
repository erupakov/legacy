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
using Cryptany.Core.DPO;
using Cryptany.Core.DPO.MetaObjects.Attributes;

namespace Cryptany.Core.ConfigOM
{
    [Serializable]
    [DbSchema("services")]
    [Table("ChannelState")]
    public class ChannelState : EntityBase
    {

        [Relation(RelationType.OneToOne, typeof(Channel), "ID")]
        [FieldName("ChannelId")]
        public Channel Channel
        {
            get
            {
                return GetValue<Channel>("Channel");
            }
            set
            {
                SetValue("Channel", value);
            }
        }

        public ChannelPublishState PublishState
        {
            get
            {
                return (ChannelPublishState)GetValue<byte>("PublishState");
            }
            set
            {
                SetValue("PublishState", (byte)value);
            }
        }

        public string UserName
        {
            get
            {
                return GetValue<string>("UserName");
            }
            set
            {
                SetValue("UserName", value);
            }
        }

        public DateTime? PublishTime
        {
            get
            {
                return GetValue<DateTime>("PublishTime");
            }
            set
            {
                SetValue("PublishTime", value);
            }

        }
    }
}
