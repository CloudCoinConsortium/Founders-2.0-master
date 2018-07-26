using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Founders.Utils
{
    public class ExportStack
    {
        public ExportStack(CloudCoinCore.Stack stack)
        {
            Cloudcoin = new CloudCoin[stack.cc.Length];
            int i = 0;
            foreach(var coin in stack.cc)
            {
                CloudCoin cloudcoin = new CloudCoin(new CloudCoinCore.CloudCoin());
                cloudcoin.an = coin.an;
                cloudcoin.sn = coin.sn;
                cloudcoin.nn = coin.nn;
                cloudcoin.pown = coin.pown;
                cloudcoin.aoid = coin.aoid;
                cloudcoin.ed = coin.ed;

                Cloudcoin[i++] = cloudcoin;
            }
        }
        [JsonProperty("cloudcoin")]
        public CloudCoin[] Cloudcoin { get; set; }

    }

    public class CloudCoin
    {
        public CloudCoin (CloudCoinCore.CloudCoin coin)
        {
            this.nn = coin.nn;
            this.an = coin.an;
            this.sn = coin.sn;
            this.pown = coin.pown;
            this.aoid = coin.aoid;
            this.ed = coin.ed;

        }
        [JsonProperty("nn")]
        public int nn { get; set; }

        [JsonProperty("sn")]
        public int sn { get; set; }

        [JsonProperty("an")]
        public List<string> an { get; set; }

        [JsonProperty("ed")]
        public string ed { get; set; }

        [JsonProperty("pown")]
        public string pown { get; set; }

        [JsonProperty("aoid")]
        public List<string> aoid { get; set; }

    }
}
