﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Web;

namespace QPS_Web1._CSHARP.Class
{
    public class QpiroJSON
    {
        public QpiroJSON()
        {
            Data = new List<object>();
        }

        [JsonProperty("error")]
        public object Error { get { if (this.Message == null) { return false; } else { Data = null; return true; } } }

        [JsonProperty("message")]
        public object Message { get; set; }

        [JsonProperty("data")]
        public new List<object> Data { get; set; }

        [JsonProperty("time")]
        public object Time { get; set; }
    }
}