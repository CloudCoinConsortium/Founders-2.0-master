﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;
using CloudCoinCoreDirectory;
using System.Net;
using Newtonsoft.Json;
using Founders;

namespace CloudCoinCore
{
    public class RAIDA
    {
        /*
         * 
         * This Class Contains and abstracts the properties of RAIDA network.
         * */
        public static RAIDA MainNetwork;
        public Node[] nodes = new Node[Config.NodeCount];
        public IFileSystem FS;
        public static IFileSystem FileSystem;
        public CloudCoin coin;
        public IEnumerable<CloudCoin> coins;
        public MultiDetectRequest multiRequest;
        public Network network;
        public int NetworkNumber=1;
        public static List<RAIDA> networks = new List<RAIDA>();
        public static RAIDA ActiveRAIDA;
        public static string Workspace;
        public static SimpleLogger logger;

        // Singleton Pattern implemented using private constructor 
        // This allows only one instance of RAIDA per application

        private RAIDA()
        {
            FS = RAIDA.FileSystem;
            for(int i = 0; i < Config.NodeCount; i++)
            {
                nodes[i] = new Node(i+1);
            }                   
        }

        private RAIDA(Network network)
        {
            nodes = new Node[network.raida.Length];
            this.NetworkNumber = network.nn;
            this.network = network;
            for (int i = 0; i < nodes.Length; i++)
            {
                nodes[i] = new Node(i + 1,network.raida[i]);
            }
        }

        // This method was introduced breaking the previously used Singleton pattern.
        // This was done in order to support multiple networks concurrently.
        // We can now have multiple RAIDA objects each containing different networks
        // RAIDA details are read from Directory URL first.
        // In case of failure, it falls back to a file on the file system
        public static List<RAIDA> Instantiate()
        {
            string nodesJson = "";
            networks.Clear();
            using (WebClient client = new WebClient())
            {
                try
                {
                    nodesJson = client.DownloadString(Config.URL_DIRECTORY);

                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    if(System.IO.File.Exists("directory.json"))
                    {
                        nodesJson = System.IO.File.ReadAllText(Environment.CurrentDirectory + @"\directory.json");
                    }
                    else
                    {
                        Exception raidaException = new Exception("RAIDA instantiation failed. No Directory found on server or local path");
                        throw raidaException;
                    }
                }
            }

            try
            {
                RAIDADirectory dir = JsonConvert.DeserializeObject<RAIDADirectory>(nodesJson);

                foreach (var network in dir.networks)
                {
                    networks.Add(RAIDA.GetInstance(network));
                }
            }
            catch(Exception e)
            {
                Exception raidaException = new Exception("RAIDA instantiation failed. No Directory found on server or local path");
                throw raidaException;
            }
            if(networks == null )
            {
                Exception raidaException = new Exception("RAIDA instantiation failed. No Directory found on server or local path");
                throw raidaException;
            }
            if(networks.Count ==0)
            {
                Exception raidaException = new Exception("RAIDA instantiation failed. No Directory found on server or local path");
                throw raidaException;
            }
            return networks;
        }

        // Return Main RAIDA Network populated with default Nodes Addresses(Network 1)
        public static RAIDA GetInstance()
        {
            if (MainNetwork != null)
                return MainNetwork;
            else
            {
                MainNetwork = new RAIDA();
                return MainNetwork;
            }
        }

        public static RAIDA GetInstance(Network network)
        {
            RAIDA raida = new RAIDA(network);
            raida.FS = FileSystem;
                return raida;
        }
       
        public List<Func<Task>> GetEchoTasks()
        {
            var echoTasks = new List<Func<Task>>
            {

            };
            for (int i = 0; i < nodes.Length; i++)
            {
                echoTasks.Add(nodes[i].Echo);
            }
            return echoTasks;
        }

        public List<Func<Task>> GetDetectTasks(CloudCoin coin)
        {
            this.coin = coin;

            var detectTasks = new List<Func<Task>>
            {

            };
            for (int i = 0; i < nodes.Length; i++)
            {
                detectTasks.Add(nodes[i].Detect);
            }
            return detectTasks;
        }

        public static void updateLog(string message)
        {
            Console.WriteLine(message);
            logger.Info(message);
        }
        private static void printStarLine()
        {
            logger.Info("********************************************************************************");
            Console.Out.WriteLine("********************************************************************************");
        }

        public async static Task ProcessCoins(bool ChangeANs = true)
        {
            var networks = (from x in IFileSystem.importCoins
                            select x.nn).Distinct().ToList();
            printStarLine();
            updateLog("Starting CloudCoin Import.");
            updateLog("Please do not close the CloudCoin Founders program until it is finished.");
            updateLog("Otherwise it may result in loss of CloudCoins.");
            printStarLine();
            updateLog("Starting Detect..");
            printStarLine();
            TimeSpan ts = new TimeSpan();
            DateTime before = DateTime.Now;
            DateTime after;

            foreach (var nn in networks)
            {
                ActiveRAIDA = (from x in RAIDA.networks
                               where x.NetworkNumber == nn
                               select x).FirstOrDefault();
                int NetworkExists = (from x in RAIDA.networks
                                     where x.NetworkNumber == nn
                                     select x).Count();
                if (NetworkExists > 0)
                {
                    //updateLog("Starting Coins detection for Network " + nn);
                    updateLog("Depositing: Checking all authenticity numbers and changing them on the RAIDA.");

                    await ProcessNetworkCoins(nn, ChangeANs);
                    updateLog("Coins detection for Network " + nn + " Finished.");
                }
            }
            after = DateTime.Now;
            ts = after.Subtract(before);

            Debug.WriteLine("Detection Completed in:" + ts.TotalMilliseconds / 1000 + " s");
            if(ts.Milliseconds ==0)
            {
                updateLog("No coins detected in Import folder.");
            }
            updateLog("Detection Completed in:" + ts.TotalMilliseconds / 1000 + " s");
            printStarLine();
        }



        public async static Task ProcessNetworkCoins(int NetworkNumber, bool ChangeANS = true)
        {
            IFileSystem FS = FileSystem;
            FileSystem.LoadFileSystem();
            FileSystem.DetectPreProcessing();

            var predetectCoins = FS.LoadFolderCoins(FS.PreDetectFolder);
            predetectCoins = (from x in predetectCoins
                              where x.nn == NetworkNumber
                              select x).ToList();

            IFileSystem.predetectCoins = predetectCoins;

            #region Check Existing coins and skip them

            IEnumerable<CloudCoin> bankCoins = IFileSystem.bankCoins;
            IEnumerable<CloudCoin> frackedCoins1 = IFileSystem.frackedCoins;

            var bCoins = bankCoins.ToList();
            bCoins.AddRange(frackedCoins1);
            //bankCoins.ToList().AddRange(frackedCoins1);

            var totalBankCoins = bCoins;

            var snList = (from x in totalBankCoins
                          where x.nn== NetworkNumber
                          select x.sn).ToList();

            var newCoins = from x in predetectCoins where !snList.Contains(x.sn) select x;
            var existingCoins = from x in predetectCoins where snList.Contains(x.sn) select x;

            foreach (var coin in existingCoins)
            {
                updateLog("Found coin SN:" + coin.sn + " in folders. Skipping detection of the coin SN:" + coin.sn);
                FS.MoveFile(FS.PreDetectFolder + coin.FileName + ".stack", FS.TrashFolder + coin.FileName + ".stack", IFileSystem.FileMoveOptions.Replace);
            }

            predetectCoins = newCoins.ToList() ;

            #endregion
            RAIDA raida = (from x in networks
                           where x.NetworkNumber == NetworkNumber
                           select x).FirstOrDefault();
            if (raida == null)
                return;
            // Process Coins in Lots of 200. Can be changed from Config File
            int LotCount = predetectCoins.Count() / Config.MultiDetectLoad;
            if (predetectCoins.Count() % Config.MultiDetectLoad > 0) LotCount++;
            ProgressChangedEventArgs pge = new ProgressChangedEventArgs();

            int CoinCount = 0;
            int totalCoinCount = predetectCoins.Count();
            for (int i = 0; i < LotCount; i++)
            {
                //Pick up 200 Coins and send them to RAIDA
                var coins = predetectCoins.Skip(i * Config.MultiDetectLoad).Take(Config.MultiDetectLoad);
                try
                {
                    raida.coins = coins;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
                var tasks = raida.GetMultiDetectTasks(coins.ToArray(), Config.milliSecondsToTimeOut, ChangeANS);
                try
                {
                    string requestFileName = Utils.RandomString(16).ToLower() + DateTime.Now.ToString("yyyyMMddHHmmss") + ".stack";
                    // Write Request To file before detect
                    FS.WriteCoinsToFile(coins, FS.RequestsFolder + requestFileName);
                    logger.Info("Request: " + FS.RequestsFolder + requestFileName);
                    await Task.WhenAll(tasks.AsParallel().Select(async task => await task()));
                    int j = 0;
                    foreach (var coin in coins)
                    {
                        coin.pown = "";
                        for (int k = 0; k < CloudCoinCore.Config.NodeCount; k++)
                        {
                            coin.response[k] = raida.nodes[k].MultiResponse.responses[j];
                            coin.pown += coin.response[k].outcome.Substring(0, 1);
                        }
                        int countp = coin.response.Where(x => x.outcome == "pass").Count();
                        int countf = coin.response.Where(x => x.outcome == "fail").Count();
                        coin.PassCount = countp;
                        coin.FailCount = countf;
                        CoinCount++;


                        logger.Info("No. " + CoinCount + ". Coin Deteced. S. No. - " + coin.sn + ". Pass Count - " + coin.PassCount + ". Fail Count  - " + coin.FailCount + ". Result - " + coin.DetectionResult + "." + coin.pown);
                        Debug.WriteLine("Coin Deteced. S. No. - " + coin.sn + ". Pass Count - " + coin.PassCount + ". Fail Count  - " + coin.FailCount + ". Result - " + coin.DetectionResult);
                        //coin.sortToFolder();
                        pge.MinorProgress = (CoinCount) * 100 / totalCoinCount;
                        Debug.WriteLine("Minor Progress- " + pge.MinorProgress);
                        raida.OnProgressChanged(pge);
                        coin.doPostProcessing();
                        j++;
                    }
                    pge.MinorProgress = (CoinCount - 1) * 100 / totalCoinCount;
                    Debug.WriteLine("Minor Progress- " + pge.MinorProgress);
                    raida.OnProgressChanged(pge);
                    FS.WriteCoin(coins, FS.DetectedFolder, false);
                    FS.RemoveCoins(coins, FS.PreDetectFolder);

                    updateLog(pge.MinorProgress + " % of Coins on Network " + NetworkNumber + " processed.");
                    //FS.WriteCoin(coins, FS.DetectedFolder);

                }
                catch (Exception ex)
                {
                    logger.Error(ex.Message);
                    Debug.WriteLine(ex.Message);
                }


            }
            pge.MinorProgress = 100;
            Debug.WriteLine("Minor Progress- " + pge.MinorProgress);
            raida.OnProgressChanged(pge);
            var detectedCoins = FS.LoadFolderCoins(FS.DetectedFolder);
            //detectedCoins.ForEach(x => x.pown= "ppppppppppppppppppppppppp");
            updateLog("  Detection finished.");
            printStarLine();

            // Apply Sort to Folder to all detected coins at once.
            updateLog("  Starting Grading Coins..");
            //detectedCoins.ForEach(x => x.doPostProcessing());
            detectedCoins.ForEach(x => x.SortToFolder());
            updateLog("  Grading Coins Completed.");

            var passedCoins = (from x in detectedCoins
                               where x.folder == FS.BankFolder
                               select x).ToList();

            var frackedCoins = (from x in detectedCoins
                                where x.folder == FS.FrackedFolder
                                select x).ToList();

            var failedCoins = (from x in detectedCoins
                               where x.folder == FS.CounterfeitFolder
                               select x).ToList();
            var lostCoins = (from x in detectedCoins
                             where x.folder == FS.LostFolder
                             select x).ToList();
            var suspectCoins = (from x in detectedCoins
                                where x.folder == FS.SuspectFolder
                                select x).ToList();

            var dangerousCoins = (from x in detectedCoins
                                  where x.folder == FS.DangerousFolder
                                  select x).ToList();

            Debug.WriteLine("Total Passed Coins - " + (passedCoins.Count() + frackedCoins.Count()));
            Debug.WriteLine("Total Failed Coins - " + failedCoins.Count());
            updateLog("Detection and Import of the CloudCoins completed.");

            updateLog("Coin Detection finished.");
            updateLog("  Total Passed Coins - " + (passedCoins.Count() + frackedCoins.Count()) + "");
            updateLog("  Total Counterfeit Coins - " + failedCoins.Count() + "");
            updateLog("  Total Lost Coins - " + lostCoins.Count() + "");
            updateLog("  Total Suspect Coins - " + suspectCoins.Count() + "");
            updateLog("  Total Dangerous Coins - " + dangerousCoins.Count() + "");

            //Remove existing duplicate coins in Bank / Fracked
            var ExistingGoodCoins = FS.LoadFolderCoins(FS.BankFolder);
            ExistingGoodCoins.Concat(FS.LoadFolderCoins(FS.FrackedFolder));
            foreach(CloudCoin cc in ExistingGoodCoins)
            {
                var duplicateCoin = (from x in detectedCoins where x.sn == cc.sn select x).ToList();
                FS.RemoveCoins(duplicateCoin, FS.BankFolder);
                FS.RemoveCoins(duplicateCoin, FS.FrackedFolder);
            }


            // Move Coins to their respective folders after sort
            FS.MoveCoins(passedCoins, FS.DetectedFolder, FS.BankFolder, true);
            FS.MoveCoins(frackedCoins, FS.DetectedFolder, FS.FrackedFolder, true);

            FS.MoveCoins(failedCoins, FS.DetectedFolder, FS.CounterfeitFolder);
            FS.MoveCoins(lostCoins, FS.DetectedFolder, FS.LostFolder);
            FS.MoveCoins(suspectCoins, FS.DetectedFolder, FS.SuspectFolder);

            // Clean up Detected Folder
            FS.RemoveCoins(failedCoins, FS.DetectedFolder);
            FS.RemoveCoins(lostCoins, FS.DetectedFolder);
            FS.RemoveCoins(suspectCoins, FS.DetectedFolder);

            FS.MoveImportedFiles();

            //after = DateTime.Now;
            //ts = after.Subtract(before);

            //Debug.WriteLine("Detection Completed in - " + ts.TotalMilliseconds / 1000);
            //updateLog("Detection Completed in - " + ts.TotalMilliseconds / 1000);


            pge.MinorProgress = 100;
            Debug.WriteLine("Minor Progress- " + pge.MinorProgress);


        }


        public List<Func<Task>> GetMultiDetectTasks(CloudCoin[] coins, int milliSecondsToTimeOut, bool changeANs = true)
        {
            this.coins = coins;

            responseArrayMulti = new Response[Config.NodeCount, coins.Length];

            int[] nns = new int[coins.Length];
            int[] sns = new int[coins.Length];


            String[][] ans = new String[Config.NodeCount][];
            String[][] pans = new String[Config.NodeCount][];

            int[] dens = new int[coins.Length];//Denominations
                                               //Stripe the coins
            var detectTasks = new List<Func<Task>>
            {

            };

            List<Func<Task>> multiTaskList = new List<Func<Task>>();

            //List<Task<Response[]>> multiTaskList = new List<Task<Response[]>>();
            for (int i = 0; i < coins.Length; i++)//For every coin
            {
                if (changeANs)
                    coins[i].GeneratePAN();
                else
                    coins[i].SetAnsToPans();
                //coins[i].setAnsToPans();
                nns[i] = coins[i].nn;
                sns[i] = coins[i].sn;
                dens[i] = coins[i].denomination;

            }
            multiRequest = new MultiDetectRequest();
            multiRequest.timeout = Config.milliSecondsToTimeOut;
            for (int nodeNumber = 0; nodeNumber < Config.NodeCount; nodeNumber++)
            {

                ans[nodeNumber] = new String[coins.Length];
                pans[nodeNumber] = new String[coins.Length];

                for (int i = 0; i < coins.Length; i++)//For every coin
                {
                    ans[nodeNumber][i] = coins[i].an[nodeNumber];
                    pans[nodeNumber][i] = coins[i].pan[nodeNumber];

                }
                multiRequest.an[nodeNumber] = ans[nodeNumber];
                multiRequest.pan[nodeNumber] = pans[nodeNumber];
                multiRequest.nn = nns;
                multiRequest.sn = sns;
                multiRequest.d = dens;
            }


            for (int nodeNumber = 0; nodeNumber < Config.NodeCount; nodeNumber++)
            {
                detectTasks.Add(nodes[nodeNumber].MultiDetect);
            }

            return detectTasks;
        }
       
       
        public Response[] responseArray = new Response[25];

        public void GetTickets(int[] triad, String[] ans, int nn, int sn, int denomination, int milliSecondsToTimeOut)
        {
            //Console.WriteLine("Get Tickets called. ");
            var t00 = GetTicket(0, triad[00], nn, sn, ans[00], denomination);
            var t01 = GetTicket(1, triad[01], nn, sn, ans[01], denomination);
            var t02 = GetTicket(2, triad[02], nn, sn, ans[02], denomination);

            var taskList = new List<Task> { t00, t01, t02 };
            Task.WaitAll(taskList.ToArray(), milliSecondsToTimeOut);
            try
            {
                //  CoreLogger.Log(sn + " get ticket:" + triad[00] + " " + responseArray[triad[00]].fullResponse);
                // CoreLogger.Log(sn + " get ticket:" + triad[01] + " " + responseArray[triad[01]].fullResponse);
                //  CoreLogger.Log(sn + " get ticket:" + triad[02] + " " + responseArray[triad[02]].fullResponse);
            }
            catch { }
            //Get data from the detection agents
        }//end detect coin

        public async Task GetTicket(int i, int raidaID, int nn, int sn, String an, int d)
        {
            responseArray[raidaID] = await nodes[raidaID].GetTicket(nn, sn, an, d);
        }//end get ticket
        
        public async Task DetectCoin(CloudCoin coin, int milliSecondsToTimeOut)
        {
            //Task.WaitAll(coin.detectTaskList.ToArray(),Config.milliSecondsToTimeOut);
            //Get data from the detection agents
            //Task.WaitAll(coin.detectTaskList.ToArray(), milliSecondsToTimeOut);
            await Task.WhenAll(coin.detectTaskList);
            for (int i = 0; i < Config.NodeCount; i++)
            {
                var resp = coin.response;

            }//end for each detection agent

            var counts = coin.response
                .GroupBy(item => item.outcome== "pass")
                .Select(grp => new { Number = grp.Key, Count = grp.Count() });

            var countsf = coin.response
                    .GroupBy(item => item.outcome == "fail")
                    .Select(grp => new { Number = grp.Key, Count = grp.Count() });

            Debug.WriteLine("Pass Count -" +counts.Count());
            Debug.WriteLine("Fail Count -" + countsf.Count());

            coin.SetAnsToPansIfPassed();
            coin.CalculateHP();

            coin.CalcExpirationDate();
            coin.grade();
            coin.SortToFolder();
            DetectEventArgs de = new DetectEventArgs(coin);
            OnCoinDetected(de);

        }//end detect coin

        public event EventHandler ProgressChanged;
        public event EventHandler LoggerHandler;

        public int ReadyCount { get { return nodes.Where(x => x.RAIDANodeStatus == NodeStatus.Ready).Count(); } }
        public int NotReadyCount { get { return nodes.Where(x => x.RAIDANodeStatus == NodeStatus.NotReady).Count(); } }

        public virtual void OnProgressChanged(ProgressChangedEventArgs e)
        {
            ProgressChanged?.Invoke(this, e);
        }

        public void OnLogRecieved(ProgressChangedEventArgs e)
        {
            LoggerHandler?.Invoke(this, e);
        }

        public event EventHandler CoinDetected;

        protected virtual void OnCoinDetected(DetectEventArgs e)
        {
            CoinDetected?.Invoke(this, e);
        }
        public Response[,] responseArrayMulti;

    }
}
