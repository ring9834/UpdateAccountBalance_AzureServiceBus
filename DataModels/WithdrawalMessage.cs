using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataModels
{
    public class WithdrawalMessage : AccountMessage
    {
        public string Destination { get; set; } = "CASH";
    }
}
