using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SOE.Models
{
    public class Message
    {
        [JsonProperty("to")]
        public string To { get; set; }
        [JsonProperty("from")]
        public string From { get; set; }
        [JsonProperty("content")]
        public string Content { get; set; }
        [JsonProperty("date")]
        public DateTime Date { get; set; }
    }
}
