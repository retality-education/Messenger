using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SOE.Models
{
    public class Contact
    {
        public string Id { get; set; }
        public string Nickname { get; set; }
        public DateTime LastMessageDate { get; set; }
        public string LastMessage { get; set; }
    }
}
