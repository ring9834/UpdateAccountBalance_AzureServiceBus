using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataModels
{
    public class FeeMessage : AccountMessage
    {
        public string FeeType { get; set; } = "SERVICE";
        public string? Reason { get; set; }
    }
}
