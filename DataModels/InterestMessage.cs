using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataModels
{
    public class InterestMessage: AccountMessage
    {
        public decimal InterestRate { get; set; }
        public string Period { get; set; } = "DAILY";
    }
}
