using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celebrium
{
    public class CelebriumUtils
    {
        public static string GetCelebriumName(string FileName)
        {
            return FileName.Replace("CloudCoin", "Celebrium");
        }
    }
}
