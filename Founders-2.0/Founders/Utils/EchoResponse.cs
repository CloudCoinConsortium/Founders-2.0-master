using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CloudCoinCore;
namespace CoreAPIs
{
    public class EchoResponse
    {
        public int ReadyCount = 0;
        public int NotReadyCount = 0;
        public int NetworkNumber = 0;

        public NodeEchoResponse[] responses = new NodeEchoResponse[Config.NodeCount];

    }

    public class NodeEchoResponse
    {
        public string message = "";
        public string status =  "";
        public string version = "";
        public string server = "";
        public string time = "";
    }
}
