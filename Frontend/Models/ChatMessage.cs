using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SOE.Models
{
    public class ChatMessage
    {
        public string Content { get; set; }
        public DateTime Date { get; set; }
        public bool IsIncoming { get; set; }
        public string SenderId { get; set; } // Новое свойство для ID отправителя
    }
}
