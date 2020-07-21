using GenericParsing;
using KiteConnect;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace KiteConsole_v2
{
    static class Program
    {
        static Kite kite;
        static Ticker ticker;
        static string MyPublicToken = string.Empty;
        static string MyAccessToken = string.Empty;
        static IIndicator reqIndicator = null;


        static void Main(string[] args)
        {
            //LoadHistorialData();

            foreach (System.Diagnostics.Process myProc in System.Diagnostics.Process.GetProcesses())
            {
                if (myProc.ProcessName.ToLower().Contains("chrome"))
                {
                   // myProc.Kill();
                }
            }

            string prodMode = ConfigurationManager.AppSettings["ProdMode"];

            kite = new Kite(ConfigurationManager.AppSettings["MyAPIKey"], Debug: true);
            kite.SetSessionExpiryHook(OnTokenExpire);

            if (prodMode == "0")
            {
                InitSession();
            }
            else
            {
                string connectionString = ConfigurationManager.AppSettings["KiteDB"];
                SqlConnection conn = new SqlConnection(connectionString);
                conn.Open();
                SqlCommand command = new SqlCommand("SELECT [MyAccessToken] FROM [dbo].[KiteSettings] WHERE MyUserId = 'RN5119'", conn);
                command.CommandType = System.Data.CommandType.Text;
                MyAccessToken = command.ExecuteScalar().ToString();
                conn.Close();
            }

            kite.SetAccessToken(MyAccessToken);


            //initTicker();
            UpdateTodayMargin(kite);
            
            Initiate(string.Empty, kite);
            Console.ReadLine();
        }


        private static async void UpdateTodayMargin(Kite kite)
        {
            //GetFutureMargin
            Margins[] margins = null;
            HttpClient client = new HttpClient();
            HttpResponseMessage response = await client.GetAsync("https://api.kite.trade/margins/futures");

            if (response.IsSuccessStatusCode)
            {
                string response1 = await response.Content.ReadAsStringAsync();
                margins = JsonConvert.DeserializeObject<Margins[]>(response1);
            }

            string connectionString = ConfigurationManager.AppSettings["KiteDB"];
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string commandText = "SELECT TradingSymbol FROM POSTIONSETTINGS";
                SqlCommand command = new SqlCommand(commandText, conn)
                {
                    CommandType = System.Data.CommandType.Text
                };
                DataTable dt = new DataTable();
                SqlDataAdapter da = new SqlDataAdapter(command);
                da.Fill(dt);
                if (margins != null)
                {
                    foreach (DataRow dr in dt.Rows)
                    {
                        string columnName = dr["TradingSymbol"].ToString();
                        foreach (Margins margin in margins)
                        {
                            if (margin.TradingSymbol == columnName)
                            {

                                commandText = "UPDATE PostionSettings SET MARGIN=" + margin.nrml_margin + " WHERE TradingSymbol ='" + columnName + "'";
                                command = new SqlCommand(commandText, conn)
                                {
                                    CommandType = System.Data.CommandType.Text
                                };
                                command.ExecuteNonQuery();

                            }
                        }
                    }
                }
                conn.Close();
            }

        }

        private static void initTicker()
        {
            ticker = new Ticker(ConfigurationManager.AppSettings["MyAPIKey"], MyAccessToken);

            ticker.OnTick += OnTick;
            ticker.OnReconnect += OnReconnect;
            ticker.OnNoReconnect += OnNoReconnect;
            ticker.OnError += OnError;
            ticker.OnClose += OnClose;
            ticker.OnConnect += OnConnect;
            ticker.OnOrderUpdate += OnOrderUpdate;

            ticker.EnableReconnect(Interval: 5, Retries: 50);
            ticker.Connect();

            // Subscribing to NIFTY50 and setting mode to LTP
            ticker.Subscribe(Tokens: new UInt32[] { 12111106 });
            ticker.SetMode(Tokens: new UInt32[] { 12111106 }, Mode: Constants.MODE_FULL);
        }
        // Example onTick handler
        private static void onTick(Tick TickData)
        {
            Console.WriteLine("LTP: " + TickData.LastPrice);
        }


        private static void InitSession()
        {
            MyAccessToken = "kgch9igQE2urUo9OkGR4kjNDRxuTWB55";
        }

        private static void Initiate(string intervalPeriod, Kite kite)
        {
            InitiateIndicator(intervalPeriod, kite);
        }
        private static readonly Object thisLockIndicator = new object();
        private static void InitiateIndicator(string intervalPeriod, Kite kite)
        {
            try
            {
                Task.Run(() =>
                {
                    lock (thisLockIndicator)
                    {
                        while (true)
                        {
                            string connectionString = ConfigurationManager.AppSettings["KiteDB"];
                            SqlConnection conn = new SqlConnection(connectionString);
                            conn.Open();
                            SqlDataAdapter da = new SqlDataAdapter("SELECT * FROM PostionSettings WHERE Active = 1", conn);
                            DataTable dt = new DataTable();
                            da.Fill(dt);
                            conn.Close();

                            foreach (DataRow dr in dt.Rows)
                            {
                                Settings positionSettings = new Settings
                                {
                                    TradingSymbol = dr["TradingSymbol"].ToString(),
                                    Exchange = dr["Exchange"].ToString(),
                                    InstrumentToken = dr["InstrumentToken"].ToString(),
                                    ParentInstrument = dr["ParentInstrument"].ToString(),
                                    TradeOnCrossOver = Convert.ToBoolean(dr["TradeOnCrossOver"]),
                                    LongEMA = Convert.ToDecimal(dr["LongEMA"]),
                                    ShortEMA = Convert.ToDecimal(dr["ShortEMA"]),
                                    TradeOnCrossOverUpdate = Convert.ToBoolean(dr["TradeOnCrossOverUpdate"]),
                                    PMode = (dr["FMode"].ToString() == string.Empty) ? IndicatorMode.None : (IndicatorMode)Enum.Parse(typeof(IndicatorMode), dr["FMode"].ToString()), //dr["FMode"].ToString(),
                                    StableMode = (dr["StableMode"].ToString() == string.Empty) ? IndicatorMode.None : (IndicatorMode)Enum.Parse(typeof(IndicatorMode), dr["StableMode"].ToString()), //dr["FMode"].ToString(),
                                    LastRecorded = Convert.ToDateTime(dr["LastRecorded"]),
                                    StableTime = Convert.ToInt32(dr["StableTime"]),
                                    MaxPostion = Convert.ToInt32(dr["MaxPostion"]),
                                    BuyOnlyOption = Convert.ToBoolean(dr["BuyOnlyOption"]),
                                    Target = Convert.ToInt32(dr["Target"]),
                                    IsTargetAchieved = Convert.ToBoolean(dr["IsTargetAchieved"]),
                                    LotSize = Convert.ToInt32(dr["LotSize"]),
                                    LastCrossOverTime = Convert.ToDateTime(dr["LastCrossOverTime"]),
                                    Indicator = dr["Indicator"].ToString(),
                                    SquareOffTarget = Convert.ToInt32(dr["SquareOffTarget"]),
                                    TotalPosition = Convert.ToInt32(dr["TotalPosition"]),
                                    Expiry = Convert.ToDateTime(dr["Expiry"]),
                                    Interval = Convert.ToString(dr["Interval"]),
                                    PointsInfo = Convert.ToString(dr["PointsInfo"]),
                                    Margin = Convert.ToDecimal(dr["Margin"]),
                                    IndicatorParmOne = Convert.ToDouble(dr["IndicatorParmOne"]),
                                    TradeAmount = Convert.ToInt32(dr["TradeAmount"]),
                                    InstrumentType = Convert.ToString(dr["InstrumentType"]),
                                    LastUpdateTime = Convert.ToDateTime(dr["LastUpdateTime"]),
                                    CreateTime = Convert.ToDateTime(dr["CreateTime"]),
                                    IsRSIEnabled = Convert.ToBoolean(dr["IsRSIEnabled"]),
                                    IsSTEnabled = Convert.ToBoolean(dr["IsSTEnabled"]),
                                    IsMACDEnabled = Convert.ToBoolean(dr["IsMACDEnabled"]),
                                    STBasicSetting = Convert.ToDecimal(dr["STBasicSetting"]),
                                    Active = Convert.ToBoolean(dr["Active"])
                                };

                                positionSettings.runSettings = new RunSettings();
                                positionSettings.runSettings = DAL.GetSettingsDataFromDB(positionSettings.ParentInstrument);
                                if (string.IsNullOrEmpty(positionSettings.Indicator)) positionSettings.Indicator = "EMA";
                                if (true)
                                {
                                    //SettingsCalc.IndicatorCheck_v1(positionSettings);
                                    try
                                    {
                                        IndicatorCheck(positionSettings);
                                    }
                                    catch (Exception ex)
                                    {
                                        ExceptionLog(ex);
                                        Thread.Sleep(1000);
                                    }
                                }
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                ExceptionLog(ex);
                Thread.Sleep(1000);
                InitiateIndicator(null, kite);
            }
        }

        private static void IndicatorCheck(Settings positionSettings)
        {
            List<Attributes> attributes = null;
            List<Historical> historical;

            if (true)
            {
                //positionSettings.ParentInstrument = "13400066";
                Thread.Sleep(1000);

                //positionSettings.ParentInstrument = "884737";
                //positionSettings.Interval = "30minute";
                historical = Program.GetHistoricalData(kite, positionSettings.ParentInstrument, GetIndianDateTime().AddDays(-180).AddDays(0), GetIndianDateTime().AddDays(0), positionSettings.Interval, false);
                //historical = Program.GetHistoricalData(kite, positionSettings.ParentInstrument, GetIndianDateTime().AddDays(-360), GetIndianDateTime().AddDays(-180), positionSettings.Interval, false);
                //historical = Program.GetHistoricalData(kite, positionSettings.ParentInstrument, GetIndianDateTime().AddDays(-540), GetIndianDateTime().AddDays(-360), positionSettings.Interval, false);
                ///historical = Program.GetHistoricalData(kite, positionSettings.ParentInstrument, GetIndianDateTime().AddDays(-860), GetIndianDateTime().AddDays(-700), positionSettings.Interval, false);

                if (historical.Count > 0)
                {
                    attributes = historical.OrderBy(x => x.TimeStamp).Select(x => new Attributes
                    {
                        Close = x.Close,
                        TimeStamp = x.TimeStamp,
                        High = x.High,
                        Low = x.Low,
                        Open = x.Open,
                        Volume = x.Volume
                    }).OrderBy(x => x.TimeStamp).ToList();
                }
                else { return; }
            }
            else
            {
                positionSettings.ParentInstrument = "81153";

                attributes = GetDataFromDB(positionSettings.ParentInstrument);

                positionSettings.IsMACDEnabled = true;
                positionSettings.IsSTEnabled = true;
                positionSettings.IsRSIEnabled = true;
            }

            //
            Algov2 algo = new Algov2();
            algo.Main(kite, positionSettings);

            //

            Position position = new Position()
            {
                PositionAttributes = attributes,
                PositionSettings = positionSettings
            };

            if (position.PositionAttributes != null && position.PositionAttributes.Count > 0)
            {

                if (true)
                {
                    reqIndicator = new Cloud();
                    position = reqIndicator.StartLooking(position.PositionSettings, kite, position.PositionAttributes);
                }

               


                if (true)
                {
                    reqIndicator = new MFI();
                    position = reqIndicator.StartLooking(position.PositionSettings, kite, position.PositionAttributes);
                }
                //if (true)
                //{
                //    reqIndicator = new HA();
                //    position = reqIndicator.StartLooking(position.PositionSettings, kite, position.PositionAttributes);
                //}

                if (!positionSettings.IsAdxEnabled)
                {
                    reqIndicator = new ADX();
                    position = reqIndicator.StartLooking(position.PositionSettings, kite, position.PositionAttributes);
                }

                if (positionSettings.IsMACDEnabled)
                {
                    reqIndicator = new MACD();
                    position = reqIndicator.StartLooking(position.PositionSettings, kite, position.PositionAttributes);
                }

                if (positionSettings.IsSTEnabled)
                {
                    reqIndicator = new SuperTrend();
                    //positionSettings.IsTestRun = true;
                    position.PositionSettings.runSettings.STMultiplier = 3M;
                    position.PositionSettings.runSettings.STBasic = 3M;
                    //position.PositionSettings.runSettings.STMultiplier = 2.5M;
                    //position.PositionSettings.runSettings.STBasic = 2.5M;
                    position.PositionSettings.runSettings.STPeriod = 14;

                    //positionSettings.IndicatorParmOne = 2.5;
                    //positionSettings.STBasicSetting = 2.5M;
                    if (positionSettings.IsTestRun)
                    {
                        RunSTBackTest(position.PositionAttributes, position.PositionSettings, position);
                    }
                    //attributes = GetDataFromDB(positionSettings.ParentInstrument);
                    //position.PositionAttributes = null;
                    //position.PositionAttributes = attributes;

                    position = reqIndicator.StartLooking(position.PositionSettings, kite, position.PositionAttributes);

                    position.PositionSettings.runSettings.STMultiplier = 2M;
                    position.PositionSettings.runSettings.STBasic = 2M;
                    position.PositionSettings.runSettings.STIteration = 2;
                    position = reqIndicator.StartLooking(position.PositionSettings, kite, position.PositionAttributes);

                    position.PositionSettings.runSettings.STMultiplier = 4M;
                    position.PositionSettings.runSettings.STBasic = 4M;
                    position.PositionSettings.runSettings.STIteration = 3;
                    position = reqIndicator.StartLooking(position.PositionSettings, kite, position.PositionAttributes);

                }

                if (positionSettings.IsRSIEnabled)
                {
                    reqIndicator = new RSIv1();
                    position = reqIndicator.StartLooking(position.PositionSettings, kite, position.PositionAttributes);
                }


                position.PositionSettings.MacdNoise = 2;

                position.PositionSettings.RiskLevel = 1;
                //Analysis_v5(position, 0 * 30, position.PositionSettings.MacdNoise);

                if (position.PositionSettings.RiskLevel == 1)
                {
                    Analysis_Risk1(position, 0 * 30, position.PositionSettings.MacdNoise);
                }

                if (position.PositionSettings.RiskLevel == 2)
                {
                    Analysis_Risk2(position, 0 * 30, position.PositionSettings.MacdNoise);
                }

                if (position.PositionSettings.RiskLevel == 3)
                {
                    Analysis_Risk3(position, 0 * 30, position.PositionSettings.MacdNoise);
                }

                if (position.PositionSettings.RiskLevel == 4)
                {
                    Analysis_Risk4(position, 0 * 30, position.PositionSettings.MacdNoise);
                }

                if (position.PositionSettings.RiskLevel == 5)//SuperTrend
                {
                    Analysis_Risk5(position, 0 * 30, position.PositionSettings.MacdNoise);
                    CalculatePLWaitFunc(position);
                }

                if (position.PositionSettings.RiskLevel == 6)// with inchmo cloud
                {
                    Analysis_Risk6(position, 0 * 30, position.PositionSettings.MacdNoise);
                }

                //Analysis_v6(position, 0 * 30, position.PositionSettings.MacdNoise);

                //Calculate PL

                CalculatePL(position);

                //Assign Mode
                AssignMode(position);
                ExecuteTradeByMargin(position);
            }
        }



        private static async Task RunSTBackTest(List<Attributes> attributes, Settings positionSettings, Position position)
        {

            var tasks = new List<Task<bool>>();

            positionSettings.TotalPL = 0;
            positionSettings.IndicatorParmOne = 1;
            Parallel.Invoke(() =>
            {
                for (double j = 1; j < 5; j += 0.1)
                {
                    for (double k = 1; k < 5; k += 0.1)
                    {
                        RunSTBackTest1(attributes, positionSettings, position, j, k);
                        //Parallel.Invoke(() => { RunSTBackTest1(attributes, positionSettings, position, j, k); });
                    }
                }
            });


            foreach (var task in await Task.WhenAll(tasks))
            {
                if (task)
                {
                    Console.WriteLine("Ending Process {0}", task);
                }
            }

            positionSettings.IndicatorParmOne = Convert.ToDouble(positionSettings.PointsInfo);
        }
        private static async Task<bool> RunSTBackTest1(List<Attributes> attributes, Settings positionSettings, Position position, double j, double k)
        {
            positionSettings.TotalPL = 0;

            position.PositionSettings.IndicatorParmOne = j;
            position.PositionSettings.STBasicSetting = Convert.ToDecimal(k);
            //
            position.PositionSettings.IsDepth = false;
            //attributes = GetDataFromDB(positionSettings.ParentInstrument);
            position.PositionAttributes = null;
            position.PositionAttributes = attributes;
            SuperTrendTest st = new SuperTrendTest();
            Position x = await st.StartLooking(position.PositionSettings, kite, position.PositionAttributes);
            return true;
        }


        private static async Task RunSTBackTestDefault(List<Attributes> attributes, Settings positionSettings, Position position)
        {

            var tasks = new List<Task<Tuple<int, bool>>>();

            positionSettings.TotalPL = 0;
            positionSettings.IndicatorParmOne = 1;
            for (double j = 1; j < 10; j += 0.1)
            {
                for (double k = 1; k < 10; k += 0.1)
                {
                    position.PositionSettings.IndicatorParmOne = j;
                    position.PositionSettings.STBasicSetting = Convert.ToDecimal(k);
                    //
                    position.PositionSettings.IsDepth = false;
                    attributes = GetDataFromDB(positionSettings.ParentInstrument);
                    position.PositionAttributes = null;
                    position.PositionAttributes = attributes;
                    position = reqIndicator.StartLooking(position.PositionSettings, kite, position.PositionAttributes);
                }
            }
            positionSettings.IndicatorParmOne = Convert.ToDouble(positionSettings.PointsInfo);
        }

        private static void AssignMode(Position position)
        {
            //MACD, ST, RSI Enabled
            Settings positionSettings = position.PositionSettings;
            if (positionSettings.IsSTEnabled && positionSettings.IsRSIEnabled && positionSettings.IsMACDEnabled)
            {
                positionSettings.CurrentMode = position.PositionAttributes[position.PositionAttributes.Count - 2].Mode;
                positionSettings.CMode = position.PositionAttributes[position.PositionAttributes.Count - 2].Mode;
            }
            //ST Only Enabled
            if (positionSettings.IsSTEnabled && !positionSettings.IsRSIEnabled && !positionSettings.IsMACDEnabled)
            {
                positionSettings.CurrentMode = position.PositionAttributes[position.PositionAttributes.Count - 2].STMode;
                positionSettings.CMode = position.PositionAttributes[position.PositionAttributes.Count - 2].STMode;
            }
            //MACD Only Enabled
            if (!positionSettings.IsSTEnabled && !positionSettings.IsRSIEnabled && positionSettings.IsMACDEnabled)
            {
                positionSettings.CurrentMode = position.PositionAttributes[position.PositionAttributes.Count - 2].MACDMode;
                positionSettings.CMode = position.PositionAttributes[position.PositionAttributes.Count - 2].MACDMode;
            }
        }

        public static void CalculatePL(Position position)
        {
            string connectionString = ConfigurationManager.AppSettings["KiteDB"];
            SqlConnection conn = new SqlConnection(connectionString);
            conn.Open();
            DateTime TradeStartDateTime = DateTime.Today, TradeEndDateTime = DateTime.Today;
            List<Attributes> Attributes = position.PositionAttributes;
            var pls = Attributes.Count > 28 ? Attributes[29].Close : 0;
            decimal currentPL = 00;
            int totalTrades = 0;
            decimal totalPL = 00;
            foreach (Attributes att in Attributes)
            {
                int i = Attributes.IndexOf(att);
                int count = 1;
                if (i > 28 && Attributes[i].Mode != Attributes[i - 1].Mode)
                {
                    //New
                    decimal ple;
                    ple = (Attributes.Count - 1 == i) ? Attributes[i].Open : Attributes[i + 1].Open;
                    TradeEndDateTime = (Attributes.Count - 1 == i) ? Attributes[i].TimeStamp : Attributes[i + 1].TimeStamp;

                    if (Attributes[i - 1].Mode == IndicatorMode.Buy)
                    {
                        Attributes[i].PL = Decimal.Subtract(ple, pls);
                    }
                    else if (Attributes[i - 1].Mode == IndicatorMode.Sell || Attributes[i].Mode == IndicatorMode.SellNWait)
                    {
                        Attributes[i].PL = Decimal.Subtract(pls, ple);
                    }

                    totalPL += Attributes[i].PL;
                    totalTrades += count;
                    if (position.PositionSettings.IsDepth)
                        LogBackTest(position, pls, ple, Attributes[i], conn, totalTrades, totalPL, TradeStartDateTime, TradeEndDateTime, IndicatorMode.None);

                    //New
                    if (Attributes[i].Mode == IndicatorMode.SellNWait)
                    {
                        pls = (Attributes.Count - 1 == i) ? Attributes[i].Open : Attributes[i + 1].Open;
                        pls = 0;
                    }
                    else
                        //End new
                        pls = (Attributes.Count - 1 == i) ? Attributes[i].Open : Attributes[i + 1].Open;
                    TradeStartDateTime = (Attributes.Count - 1 == i) ? Attributes[i].TimeStamp : Attributes[i + 1].TimeStamp;

                }
                if (Attributes[Attributes.Count - 1].Mode == IndicatorMode.Buy)
                    currentPL = Decimal.Subtract(Attributes[Attributes.Count - 1].Close, pls);
                else if (Attributes[Attributes.Count - 1].Mode == IndicatorMode.Sell)
                    currentPL = Decimal.Subtract(pls, Attributes[Attributes.Count - 1].Close);
            }
            if (!position.PositionSettings.IsDepth)
                Program.LogBackTest(position, 0, 0, Attributes[0], conn, totalTrades, totalPL, TradeStartDateTime, TradeEndDateTime, Attributes[0].Mode);


            totalPL = 0;
            int count1 = 0;
            foreach (Attributes st in Attributes)
            {
                if (st.TimeStamp > Convert.ToDateTime("01-02-2019"))// && st.TimeStamp <= GetIndianDateTime().AddDays(-30))
                {

                    if (st.PL != 0)
                        totalPL += st.PL;
                    if (st.PL != 0)
                        count1 += 1;
                }
            }

            totalPL = 0;
            count1 = 0;
            //foreach (Attributes st in Attributes)
            //{
            //    if (st.TimeStamp > Convert.ToDateTime("01-01-2017") && st.TimeStamp < Convert.ToDateTime("01-01-2018"))
            //    {

            //        if (st.PL != 0)
            //            totalPL += st.PL;
            //        if (st.PL != 0)
            //            count1 += 1;
            //    }
            //}

            //totalPL = 0;
            //count1 = 0;
            //foreach (Attributes st in Attributes)
            //{
            //    if (st.TimeStamp > Convert.ToDateTime("01-01-2016") && st.TimeStamp < Convert.ToDateTime("01-01-2017"))
            //    {

            //        if (st.PL != 0)
            //            totalPL += st.PL;
            //        if (st.PL != 0)
            //            count1 += 1;
            //    }
            //}


            //totalPL = 0;
            //count1 = 0;
            //foreach (Attributes st in Attributes)
            //{
            //    if (st.TimeStamp >= GetIndianDateTime().AddDays(-30))
            //    {

            //        if (st.PL != 0)
            //            totalPL += st.PL;
            //        if (st.PL != 0)
            //            count1 += 1;
            //    }
            //}

            conn.Close();

        }

        public static void LogBackTest(Position position, decimal pls, decimal ple, Attributes attributes, SqlConnection connection, int totalTrades, decimal totalPL, DateTime TradeStartDateTime, DateTime TradeEndDateTime, IndicatorMode indicatorMode = IndicatorMode.None)
        {

            string commandText = "INSERT INTO BackTestLog (TradingSymbol,Indicator,Mode,ST_Multiple, ST_Basic, Quantity,OpenPrice,ClosePrice,Points,TotalPoints,TotalTrades,IsAdxUnStable, MacdNoise,TradeStartDateTime,TradeEndDateTime) SELECT '"
                           + position.PositionSettings.TradingSymbol + "','"
                           + attributes.TestValue + "','"
                           //+ position.PositionSettings.Indicator + "','"
                           + ((indicatorMode != IndicatorMode.None) ? indicatorMode : attributes.Mode) + "','"
                           + position.PositionSettings.IndicatorParmOne + "','"
                           + position.PositionSettings.STBasicSetting + "','"
                           + position.PositionSettings.LotSize + "','"
                           + pls + "','"
                           + ple + "','"
                           + attributes.PL + "','"
                           + totalPL + "','"
                           + totalTrades + "','"
                           + attributes.IsAdxUnStable + "','"
                           + position.PositionSettings.MacdNoise + "','"
                           + TradeStartDateTime.ToString("yyyy-MM-dd HH:mm:ss") + "','"
                           + TradeEndDateTime.ToString("yyyy-MM-dd HH:mm:ss") + "'";

            SqlCommand command = new SqlCommand(commandText, connection);
            command.ExecuteNonQuery();
        }
        public static void LogBackTest(Position position, decimal pls, decimal ple, Attributes attributes, SqlConnection connection, int totalTrades, decimal totalPL, DateTime TradeStartDateTime, DateTime TradeEndDateTime, HACandle indicatorMode = HACandle.None)
        {

            string commandText = "INSERT INTO BackTestLog (TradingSymbol,Indicator,Mode,ST_Multiple, ST_Basic, Quantity,OpenPrice,ClosePrice,Points,TotalPoints,TotalTrades,IsAdxUnStable, MacdNoise,TradeStartDateTime,TradeEndDateTime) SELECT '"
                           + position.PositionSettings.TradingSymbol + "','"
                           + attributes.TestValue + "','"
                           //+ position.PositionSettings.Indicator + "','"
                           + ((indicatorMode != HACandle.None) ? indicatorMode : attributes.HACandle) + "','"
                           + position.PositionSettings.IndicatorParmOne + "','"
                           + position.PositionSettings.STBasicSetting + "','"
                           + position.PositionSettings.LotSize + "','"
                           + pls + "','"
                           + ple + "','"
                           + attributes.PL + "','"
                           + totalPL + "','"
                           + totalTrades + "','"
                           + attributes.IsAdxUnStable + "','"
                           + position.PositionSettings.MacdNoise + "','"
                           + TradeStartDateTime.ToString("yyyy-MM-dd HH:mm:ss") + "','"
                           + TradeEndDateTime.ToString("yyyy-MM-dd HH:mm:ss") + "'";

            SqlCommand command = new SqlCommand(commandText, connection);
            command.ExecuteNonQuery();
        }

        public static void LogBackTest_Algo(Position position, decimal pls, decimal ple, Attributes attributes, SqlConnection connection, int totalTrades, decimal totalPL, DateTime TradeStartDateTime, DateTime TradeEndDateTime, IndicatorMode indicatorMode = IndicatorMode.None)
        {

            string commandText = "INSERT INTO BackTestLog (TradingSymbol,Indicator,Mode,ST_Multiple, ST_Basic, Quantity,OpenPrice,ClosePrice,Points,TotalPoints,TotalTrades,IsAdxUnStable, MacdNoise,TradeStartDateTime,TradeEndDateTime) SELECT '"
                           + position.PositionSettings.TradingSymbol + "','"
                           + attributes.TestValue + "','"
                           //+ position.PositionSettings.Indicator + "','"
                           + ((indicatorMode != IndicatorMode.None) ? indicatorMode : attributes.Mode) + "','"
                           + position.PositionSettings.IndicatorParmOne + "','"
                           + position.PositionSettings.STBasicSetting + "','"
                           + position.PositionSettings.LotSize + "','"
                           + pls + "','"
                           + ple + "','"
                           + attributes.PL + "','"
                           + totalPL + "','"
                           + totalTrades + "','"
                           + attributes.IsAdxUnStable + "','"
                           + position.PositionSettings.MacdNoise + "','"
                           + TradeStartDateTime.ToString("yyyy-MM-dd HH:mm:ss") + "','"
                           + TradeEndDateTime.ToString("yyyy-MM-dd HH:mm:ss") + "'";

            SqlCommand command = new SqlCommand(commandText, connection);
            command.ExecuteNonQuery();
        }


        public static List<Attributes> GetDataFromDB(string symbol)
        {
            List<Attributes> attributes = new List<Attributes>();
            string connectionString = ConfigurationManager.AppSettings["KiteDB"];
            SqlConnection conn = new SqlConnection(connectionString);

            conn.Open();
            //SqlDataAdapter da = new SqlDataAdapter("SELECT * FROM HistoricalData WHERE InstrumentToken = '" + symbol + "'", conn);
            //SqlDataAdapter da = new SqlDataAdapter("SELECT * FROM HistoricalData WHERE InstrumentToken = '" + symbol + "' AND Timestamp  >=  CONVERT(VARCHAR, '2018-01-01 00:00:00', 103)", conn);
            //SqlDataAdapter da = new SqlDataAdapter("SELECT * FROM HistoricalData WHERE InstrumentToken = '" + symbol + "' AND Timestamp  >=  CONVERT(VARCHAR, '2018-01-01 00:00:00', 103) AND Timestamp < CONVERT(VARCHAR, '2019-01-01 00:00:00', 103)", conn);
            //SqlDataAdapter da = new SqlDataAdapter("SELECT * FROM HistoricalData WHERE InstrumentToken = '" + symbol + "' AND Timestamp  >=  CONVERT(VARCHAR, '2018-01-01 00:00:00', 103) AND Timestamp < CONVERT(VARCHAR, '2018-07-01 00:00:00', 103)", conn);

            SqlDataAdapter da = new SqlDataAdapter("SELECT * FROM HistoricalData WHERE InstrumentToken = '" + symbol + "' AND Timestamp  >=  CONVERT(VARCHAR, '2017-01-01 00:00:00', 103) AND Timestamp < CONVERT(VARCHAR, '2018-01-01 00:00:00', 103)", conn);
            //SqlDataAdapter da = new SqlDataAdapter("SELECT * FROM HistoricalData WHERE InstrumentToken = '" + symbol + "' AND Timestamp  >=  CONVERT(VARCHAR, '2016-01-01 00:00:00', 103) AND Timestamp < CONVERT(VARCHAR, '2017-01-01 00:00:00', 103)", conn);

            //SqlDataAdapter da = new SqlDataAdapter("SELECT * FROM HistoricalData WHERE InstrumentToken = '" + symbol + "' AND Timestamp  >=  CONVERT(VARCHAR, '2016-01-01 00:00:00', 103) AND Timestamp < CONVERT(VARCHAR, '2019-01-01 00:00:00', 103)", conn);

            DataTable dt = new DataTable();
            da.Fill(dt);
            conn.Close();

            foreach (DataRow row in dt.Rows)
            {
                attributes.Add(new Attributes()
                {
                    Open = Convert.ToDecimal(row["Open"]),
                    High = Convert.ToDecimal(row["High"]),
                    Low = Convert.ToDecimal(row["Low"]),
                    Close = Convert.ToDecimal(row["Close"]),
                    TimeStamp = Convert.ToDateTime(row["Timestamp"]),
                    Volume = Convert.ToUInt32(row["volume"])
                });
            }
            return attributes;
        }
        private static void Analysis_v6(Position position, int days, decimal macdNoise)
        {
            List<Attributes> fAttributes = position.PositionAttributes.ToList();
            decimal startPrice = 0, endPrice = 0; DateTime startDateTime = new DateTime();
            bool isLoss = false;

            foreach (Attributes att in fAttributes)
            {
                int i = fAttributes.IndexOf(att);
                if (i > 28)
                {
                    if (att.STMode == IndicatorMode.Buy)
                    {
                        if (att.STMode == IndicatorMode.Buy && att.MACDMode == IndicatorMode.Buy)
                        {
                            att.Mode = IndicatorMode.Buy;
                        }
                        else
                            att.Mode = fAttributes[i - 1].Mode;
                    }

                    else if (att.STMode == IndicatorMode.Sell)
                    {
                        if (att.MACDMode == IndicatorMode.Sell)
                        {
                            att.Mode = IndicatorMode.Sell;
                        }
                        else
                            att.Mode = fAttributes[i - 1].Mode;
                    }


                    endPrice = att.Close;
                    if (att.Mode != fAttributes[i - 1].Mode)
                    {
                        if ((startPrice < endPrice) && att.Mode == IndicatorMode.Buy)
                        {
                            isLoss = true;
                        }
                        if ((startPrice > endPrice) && att.Mode == IndicatorMode.Sell)
                        {
                            isLoss = true;
                        }
                        startPrice = att.Close;
                        startDateTime = att.TimeStamp;
                    }
                }
            }
        }
        private static void Analysis_Risk3(Position position, int days, decimal macdNoise)
        {
            List<Attributes> fAttributes = position.PositionAttributes.ToList();
            decimal startPrice = 0, endPrice = 0; DateTime startDateTime = new DateTime();
            bool isLoss = false;

            foreach (Attributes att in fAttributes)
            {
                int i = fAttributes.IndexOf(att);
                if (i > 28)
                {

                    if (att.MFI < 20 & att.MFI != 0)
                    {
                        position.PositionSettings.BuyMFITrigger = true;
                    }

                    if (position.PositionSettings.BuyMFITrigger && att.MFI > att.MFIEMA1 && !position.PositionSettings.BuyMFITriggered)
                    {
                        position.PositionSettings.BuyMFITrigger = false;
                        position.PositionSettings.BuyMFITriggered = true;
                        att.Mode = IndicatorMode.Buy;
                    }

                    if (position.PositionSettings.BuyMFITriggered)
                    {
                        if (att.MFI > att.MFIEMA1)
                        {
                            att.Mode = IndicatorMode.Buy;
                        }
                        else
                        {
                            att.Mode = IndicatorMode.Sell;
                            position.PositionSettings.BuyMFITriggered = false;
                        }
                    }

                    if (position.PositionSettings.BuyMFITrigger && att.MFI > att.MFIEMA1)
                    {
                        position.PositionSettings.BuyMFITrigger = false;
                        position.PositionSettings.BuyMFITriggered = true;
                        att.Mode = IndicatorMode.Buy;
                    }

                }
            }
        }

        private static void Analysis_Risk4(Position position, int days, decimal macdNoise)
        {
            List<Attributes> fAttributes = position.PositionAttributes.ToList();
            decimal startPrice = 0, endPrice = 0; DateTime startDateTime = new DateTime();
            bool isLoss = false;

            foreach (Attributes att in fAttributes)
            {
                int i = fAttributes.IndexOf(att);
                if (i > 28)
                {

                    if (att.MFI < 20 & att.MFI != 0)
                    {
                        position.PositionSettings.BuyMFITrigger = true;
                    }

                    if (position.PositionSettings.BuyMFITrigger && att.MFI > att.MFIEMA1 && !position.PositionSettings.BuyMFITriggered)
                    {
                        position.PositionSettings.BuyMFITrigger = false;
                        position.PositionSettings.BuyMFITriggered = true;
                        att.Mode = IndicatorMode.Buy;
                    }

                    if (att.MACD > att.SignalLine && position.PositionSettings.BuyMFITriggered && Math.Abs(att.MACD - att.SignalLine) > 0.6M)
                    {
                        position.PositionSettings.BuyMACDTriggered = true;
                        position.PositionSettings.BuyMFITriggered = false;
                    }

                    if (position.PositionSettings.BuyMFITriggered)
                    {
                        //if (att.MFI > att.MFIEMA1)
                        //{
                        //    att.Mode = IndicatorMode.Buy;
                        //}
                        att.Mode = IndicatorMode.Buy;
                    }


                    if (position.PositionSettings.BuyMACDTriggered && att.MACD > att.SignalLine)
                    {
                        att.Mode = IndicatorMode.Buy;
                        position.PositionSettings.BuyMACDTriggered = true;
                    }

                    if (att.MACD > att.SignalLine)
                    {

                    }

                    else if (att.SignalLine > att.MACD && position.PositionSettings.BuyMACDTriggered)
                    {
                        att.Mode = IndicatorMode.Sell;
                        position.PositionSettings.BuyMACDTriggered = false;
                    }
                }
            }
        }

        private static void Analysis_Risk5(Position position, int days, decimal macdNoise)
        {
            List<Attributes> fAttributes = position.PositionAttributes.ToList();
            decimal startPrice = 0, endPrice = 0; DateTime startDateTime = new DateTime();
            bool isLoss = false;

            foreach (Attributes att in fAttributes)
            {
                int i = fAttributes.IndexOf(att);
                if (i > 28)
                {

                    if (att.STMode == IndicatorMode.Buy)
                    {
                        if (att.STMode2 == IndicatorMode.Buy)
                        {
                            if (att.STMode3 == IndicatorMode.Buy)
                            {
                                att.Mode = IndicatorMode.Buy;
                            }
                            else
                            {
                                att.Mode = IndicatorMode.None;
                            }
                        }
                        else
                        {
                            att.Mode = IndicatorMode.None;
                        }
                    }
                    else if (att.STMode == IndicatorMode.Sell)
                    {
                        if (att.STMode2 == IndicatorMode.Sell)
                        {
                            if (att.STMode3 == IndicatorMode.Sell)
                            {
                                att.Mode = IndicatorMode.Sell;
                            }
                            else
                            {
                                att.Mode = IndicatorMode.None;
                            }
                        }
                        else
                        {
                            att.Mode = IndicatorMode.None;
                        }
                    }

                    //if (att.Close == 2503M)
                    //{

                    //}
                    //if (!att.Mode.ToString().Contains(att.MACDMode.ToString()) && att.SignalLine != 0)
                    //{
                    //    att.Mode = fAttributes[i - 1].Mode;
                    //}

                }
            }
        }

        private static void Analysis_Risk6(Position position, int days, decimal macdNoise)
        {
            List<Attributes> fAttributes = position.PositionAttributes.ToList();
            decimal startPrice = 0, endPrice = 0; DateTime startDateTime = new DateTime();
            bool isLoss = false;

            foreach (Attributes att in fAttributes)
            {
                int i = fAttributes.IndexOf(att);
                if (i > 28)
                {
                    if (att.Close == 1060M)
                    {

                    }

                    if (att.Close > att.maxLeadingSpan)
                    {
                        AnalyzePriceAboveCloud(fAttributes, att, position, macdNoise, i);
                    }
                    else if (att.Close > att.minLeadingSpan && att.Close < att.maxLeadingSpan)
                    {
                        AnalyzePriceOnCloud(fAttributes, att, position, macdNoise, i);
                    }
                    else if (att.Close < att.minLeadingSpan)
                    {
                        AnalyzePriceBelowCloud(fAttributes, att, position, macdNoise, i);
                    }

                    if (att.Mode == IndicatorMode.None)
                    {
                        att.Mode = fAttributes[i - 1].Mode;
                    }

                    if (att.Mode != fAttributes[i - 1].Mode)
                    {

                    }

                }
            }
        }

        private static void AnalyzePriceBelowCloud(List<Attributes> fAttributes, Attributes att, Position position, decimal macdNoise, int i)
        {
            att.Mode = IndicatorMode.Sell;
        }

        private static void AnalyzePriceOnCloud(List<Attributes> fAttributes, Attributes att, Position position, decimal macdNoise, int i)
        {
            if (att.SignalLine > att.MACD || att.NegativeDIn > att.PostiveDIn)
            {
                if (Math.Abs(att.SignalLine - att.MACD) > 0.5M)
                    att.Mode = IndicatorMode.Sell;

                if (Math.Abs(att.NegativeDIn - att.PostiveDIn) > 3M && att.MFI < att.MFIEMA1)
                    att.Mode = IndicatorMode.Sell;

            }

            if ((att.MACD > att.SignalLine && Math.Abs(att.MACD - att.SignalLine) > 0.5M)
                || (att.PostiveDIn > att.NegativeDIn && att.MACD > att.SignalLine))
            {
                att.Mode = IndicatorMode.Buy;
            }
            if (att.PostiveDIn > att.NegativeDIn && att.SignalLine > att.MACD && Math.Abs(att.SignalLine - att.MACD) < 1)
            {
                if (Math.Abs(att.PostiveDIn - att.NegativeDIn) > 3M && att.MFI > att.MFIEMA1)
                    att.Mode = IndicatorMode.Buy;

            }

        }

        private static void AnalyzePriceAboveCloud(List<Attributes> fAttributes, Attributes att, Position position, decimal macdNoise, int i)
        {
            if (att.Open > att.maxLeadingSpan)
            {
                if (att.SignalLine > att.MACD && Math.Abs(att.SignalLine - att.MACD) < 1)
                    att.Mode = IndicatorMode.Buy;
            }
            else if (att.MACD > att.SignalLine && att.PostiveDIn > att.NegativeDIn)
            {
                att.Mode = IndicatorMode.Buy;
            }

        }

        public static void CalculatePLWaitFunc(Position position)
        {
            string connectionString = ConfigurationManager.AppSettings["KiteDB"];
            SqlConnection conn = new SqlConnection(connectionString);
            conn.Open();

            List<Attributes> Attributes = position.PositionAttributes;
            var pls = Attributes.Count > 28 ? Attributes[29].Close : 0;
            decimal currentPL = 00;
            int totalTrades = 0;
            DateTime TradeStartDateTime = DateTime.Today, TradeEndDateTime = DateTime.Today;
            decimal totalPL = 00;
            foreach (Attributes att in Attributes)
            {
                int i = Attributes.IndexOf(att);
                int count = 1;
                if (Attributes[i].Close == 2359.55M)
                {

                }
                if (i > 105 && Attributes[i].Mode != Attributes[i - 1].Mode)// && Attributes[i].Mode != IndicatorMode.None)
                {
                    if (Attributes[i - 1].Mode == IndicatorMode.None)
                    {
                        pls = Attributes[i].Close; continue;
                    }
                    decimal ple = 0;

                    if ((Attributes[i].Mode == IndicatorMode.None)
                        && (Attributes[i - 1].Mode == IndicatorMode.Sell || Attributes[i - 1].Mode == IndicatorMode.Buy))
                    {
                        ple = (Attributes.Count - 1 == i) ? Attributes[i].Open : Attributes[i + 1].Open;

                    }
                    else if ((Attributes[i - 1].Mode == IndicatorMode.None)
                        && (Attributes[i].Mode == IndicatorMode.Sell || Attributes[i].Mode == IndicatorMode.Buy))
                    {
                        pls = Attributes[i].Close; continue;
                    }
                    else if (Attributes[i].Mode == IndicatorMode.Buy && Attributes[i - 1].Mode == IndicatorMode.Sell)
                    {
                        ple = (Attributes.Count - 1 == i) ? Attributes[i].Open : Attributes[i + 1].Open;
                    }
                    else if (Attributes[i - 1].Mode == IndicatorMode.Buy && Attributes[i].Mode == IndicatorMode.Sell)
                    {
                        ple = (Attributes.Count - 1 == i) ? Attributes[i].Open : Attributes[i + 1].Open;
                    }

                    TradeEndDateTime = (Attributes.Count - 1 == i) ? Attributes[i].TimeStamp : Attributes[i + 1].TimeStamp;
                    if (Attributes[i - 1].Mode == IndicatorMode.Buy)
                    {
                        Attributes[i].PL = Decimal.Subtract(ple, pls);
                    }
                    else
                    {
                        Attributes[i].PL = Decimal.Subtract(pls, ple);
                    }
                    totalPL += Attributes[i].PL;
                    totalTrades += count;

                    position.PositionSettings.Indicator = "ST";
                    if (position.PositionSettings.IsDepth)
                        Program.LogBackTest(position, pls, ple, Attributes[i], conn, totalTrades, totalPL, TradeStartDateTime, TradeEndDateTime, Attributes[i - 1].Mode);

                    if (Attributes[i].Mode != IndicatorMode.BuyNWait && Attributes[i].Mode != IndicatorMode.SellNWait)
                        pls = (Attributes.Count - 1 == i) ? Attributes[i].Open : Attributes[i + 1].Open;

                    TradeStartDateTime = (Attributes.Count - 1 == i) ? Attributes[i].TimeStamp : Attributes[i + 1].TimeStamp;
                }

            }

            if (Attributes[Attributes.Count - 1].Mode == IndicatorMode.Buy)
                currentPL = Decimal.Subtract(Attributes[Attributes.Count - 1].Close, pls);
            else if (Attributes[Attributes.Count - 1].Mode == IndicatorMode.Sell)
                currentPL = Decimal.Subtract(pls, Attributes[Attributes.Count - 1].Close);

            if (!position.PositionSettings.IsDepth)
                Program.LogBackTest(position, 0, 0, Attributes[0], conn, totalTrades, totalPL, TradeStartDateTime, TradeEndDateTime, Attributes[0].STMode);

            conn.Close();

        }

        private static void Analysis_Risk2(Position position, int days, decimal macdNoise)
        {
            List<Attributes> fAttributes = position.PositionAttributes.ToList();
            decimal startPrice = 0, endPrice = 0; DateTime startDateTime = new DateTime();
            bool isLoss = false;

            foreach (Attributes att in fAttributes)
            {
                int i = fAttributes.IndexOf(att);
                if (i > 28)
                {
                    //Test
                    if (att.TimeStamp > GetIndianDateTime().AddDays(-1))
                    {

                    }
                    //
                    if (att.Close == 695.80M)
                    {

                    }
                    if (att.TimeStamp == Convert.ToDateTime("2019-01-30 22:45:00.000"))
                    {

                    }

                    //if (att.Adx <= 20)
                    //{
                    //    att.Mode = fAttributes[i - 1].Mode; //IndicatorMode.Sell;
                    //    continue;
                    //}
                    //MACD Convergence
                    //Decimal ConvergencePct = 0;
                    //Decimal Convergence = (
                    //    Math.Abs(CurrentTick.MACD - CurrentTick.MACDSignal) +
                    //    Math.Abs(PreviousTick.MACD - PreviousTick.MACDSignal)
                    //);

                    //if (Convergence > 0.001M)
                    //{
                    //    ConvergencePct = Math.Abs(Convergence / CurrentTick.MACD);
                    //}

                    if (fAttributes[i - 1].Mode == IndicatorMode.None)
                    {


                    }
                    if (fAttributes[i].TimeStamp >= Convert.ToDateTime("06-02-2018  09:45:00"))
                    {

                    }
                    if (fAttributes[i].Close == 2792)
                    {

                    }
                    decimal curdiff = Math.Abs(Math.Abs(fAttributes[i].MACD) - Math.Abs(fAttributes[i].SignalLine));
                    decimal prevdiff = Math.Abs(Math.Abs(fAttributes[i - 1].MACD) - Math.Abs(fAttributes[i - 1].SignalLine));

                    decimal currCon = curdiff / Math.Abs(fAttributes[i].MACD);
                    decimal prevCon = prevdiff / Math.Abs(fAttributes[i - 1].MACD);



                    if (position.PositionSettings.Rule2 && att.RSIMode == IndicatorMode.Buy
                            && att.MACDMode == IndicatorMode.Buy
                            //&& Math.Abs(decimal.Subtract(Math.Abs(att.MACD), Math.Abs(att.SignalLine))) >= macdNoise
                            && att.MFI > att.MFIEMA1
                            //&& att.Close > position.PositionSettings.maxLeadingSpan 
                            && att.PostiveDIn > att.NegativeDIn && att.STMode == IndicatorMode.Sell)
                    {
                        position.PositionSettings.BuyonShortRule1 = true;
                        att.Mode = IndicatorMode.Buy;
                        continue;
                    }

                    if (att.MFI > att.MFIEMA1 && att.MFIEMA1 > 0)
                    {
                        position.PositionSettings.MFIBuyCount += 1;
                    }
                    else
                    {
                        if (position.PositionSettings.MFIBuyCount > 10)
                        {

                        }
                        position.PositionSettings.MFIBuyCount = 0;
                    }

                    if (att.STMode == IndicatorMode.Buy)
                    {
                        if (position.PositionSettings.BuyRSITriggered || position.PositionSettings.BuyRSITrigger)
                        {
                            position.PositionSettings.BuyRSITrigger = false;
                            position.PositionSettings.BuyRSITriggered = false;
                        }
                        if (position.PositionSettings.BuyMFITrigger || position.PositionSettings.BuyMFITriggered)
                        {
                            position.PositionSettings.BuyMFITrigger = false;
                            position.PositionSettings.BuyMFITriggered = false;
                        }

                        if (position.PositionSettings.Rule1 && att.STMode == IndicatorMode.Buy //&& att.RSIMode == IndicatorMode.Buy 
                            && att.MACDMode == IndicatorMode.Buy && att.AdxMode == IndicatorMode.Buy
                            && (fAttributes[i - 1].AdxMode == IndicatorMode.Sell || fAttributes[i - 2].AdxMode == IndicatorMode.Sell)
                            && Math.Abs(decimal.Subtract(att.PostiveDIn, att.NegativeDIn)) >= 4
                            && att.MFI > att.MFIEMA1)
                        {
                            att.Mode = IndicatorMode.Buy;
                        }

                        else if (position.PositionSettings.Rule2 && att.IsNewSFSignal && att.STMode == IndicatorMode.Buy && att.RSIMode == IndicatorMode.Buy
                            && att.MACDMode == IndicatorMode.Buy
                            ///&& Math.Abs(decimal.Subtract(Math.Abs(att.MACD), Math.Abs(att.SignalLine))) >= macdNoise 
                            && att.MFI > att.MFIEMA1)
                        {
                            att.Mode = IndicatorMode.Buy;
                        }

                        else if (position.PositionSettings.Rule3 && att.STMode == IndicatorMode.Buy && att.RSIMode == IndicatorMode.Sell
                            && att.MACDMode == IndicatorMode.Sell
                            && Math.Abs(decimal.Subtract(att.MACD, att.SignalLine)) > 7 && att.MFI < att.MFIEMA1)
                        {
                            att.EarlyBird = true;
                            att.Mode = IndicatorMode.Sell;
                        }
                        else if (position.PositionSettings.Rule3 && att.STMode == IndicatorMode.Buy && fAttributes[i - 1].EarlyBird == true
                            && att.RSIMode == IndicatorMode.Sell && att.MACDMode == IndicatorMode.Sell && att.MFI < att.MFIEMA1)// && att.Adx < att.PostiveDIn)
                        {
                            att.EarlyBird = true;
                            att.Mode = IndicatorMode.Sell;
                        }
                        else if (position.PositionSettings.Rule5 && att.STMode == IndicatorMode.Buy && att.RSIMode == IndicatorMode.Buy && att.MACDMode == IndicatorMode.Buy
                            && Math.Abs(decimal.Subtract(att.MACD, att.SignalLine)) >= macdNoise
                            && att.MFI > att.MFIEMA1)
                        {

                            att.Mode = IndicatorMode.Buy;
                        }
                        //else if (position.PositionSettings.Rule5 && att.STMode == IndicatorMode.Buy && att.RSIMode == IndicatorMode.Buy && att.MACDMode == IndicatorMode.Buy
                        //   && (Math.Abs(decimal.Subtract(att.MACD, att.SignalLine)) >= macdNoise || att.AdxMode == IndicatorMode.Buy
                        //   && att.MFI > att.MFIEMA1))
                        //{

                        //    att.Mode = IndicatorMode.Buy;
                        //}
                        else
                            att.Mode = fAttributes[i - 1].Mode;


                        if (position.PositionSettings.BuyMFITriggered && att.Mode == IndicatorMode.Sell)
                        {
                            att.Mode = IndicatorMode.Buy;
                        }
                        else if (position.PositionSettings.BuyMFITriggered && att.Mode == IndicatorMode.Buy)
                        {
                            position.PositionSettings.BuyMFITriggered = false;
                        }

                        if (true)
                        {
                            if (true)
                            {
                                if (att.MFI > 85 && !position.PositionSettings.SellMFITrigger && !position.PositionSettings.SellMFITriggered)
                                {
                                    position.PositionSettings.SellMFITrigger = true;
                                }

                                if (position.PositionSettings.SellMFITriggered && att.MFI > fAttributes[i - 1].MFI && att.MACD > fAttributes[i - 1].MACD)
                                {

                                }
                                if (true //&& att.MFI < fAttributes[i - 1].MFI 
                                         //&& att.MFI < 90
                                    && att.MFIEMA1 > att.MFI
                                    && att.MACD < fAttributes[i - 1].MACD
                                    //&& att.Adx < fAttributes[i - 1].Adx
                                    //&& att.RSI < fAttributes[i - 1].RSI // && att.Adx < att.PostiveDIn //&& att.MACD < fAttributes[i - 1].MACD
                                    && !position.PositionSettings.SellMFITriggered && position.PositionSettings.SellMFITrigger)
                                {
                                    if (fAttributes[i - 1].MFI > att.MFI)
                                    {

                                    }
                                    att.Mode = IndicatorMode.Sell;
                                    position.PositionSettings.SellMFITriggered = true;
                                    position.PositionSettings.SellMFITrigger = false;
                                }

                                if (position.PositionSettings.SellMFITriggered)
                                {
                                    att.Mode = IndicatorMode.Sell;
                                    //att.Mode = IndicatorMode.SellNWait;
                                }
                            }

                            if (!position.PositionSettings.SellRSITriggered && position.PositionSettings.SellRSITrigger && position.PositionSettings.Rule5
                                && att.RSIEMA1 < att.RSIEMA2 && att.MACD < att.SignalLine)
                            {
                                att.Mode = IndicatorMode.Sell;
                                position.PositionSettings.SellRSITriggered = true;
                            }
                            if (!position.PositionSettings.SellRSITrigger && position.PositionSettings.Rule5 && att.STMode == IndicatorMode.Buy && att.RSI > 80 && att.Mode == IndicatorMode.Buy)
                            {
                                position.PositionSettings.SellRSITrigger = true;
                            }

                            if (position.PositionSettings.SellRSITriggered && att.MACD > att.SignalLine)
                            {
                                position.PositionSettings.SellRSITriggered = true;
                            }
                            if (position.PositionSettings.SellRSITriggered)
                            {
                                att.Mode = IndicatorMode.Sell;
                            }

                            //Reset MFI
                            if (true && position.PositionSettings.SellMFITriggered && att.MFI > att.MFIEMA1 &&
                                att.MACD > att.SignalLine
                                && (Math.Abs(decimal.Subtract(att.MACD, att.SignalLine)) > 0
                                || att.MFI > att.MFIEMA1)
                                //&& att.ConverstionLine > att.BaseLine
                                && (fAttributes[i - 1].MACDMode == IndicatorMode.Sell || fAttributes[i - 2].MACDMode == IndicatorMode.Sell)
                                && att.AdxMode == IndicatorMode.Buy)
                            {
                                position.PositionSettings.SellMFITriggered = false;
                                att.Mode = IndicatorMode.Buy;
                                position.PositionSettings.SellRSITriggered = false;
                                position.PositionSettings.SellRSITrigger = false;
                            }

                            if (att.MACD > att.SignalLine && att.MFI > att.MFIEMA1 && att.MFI < 70 && position.PositionSettings.SellMFITriggered)
                            {
                                position.PositionSettings.SellMFITriggered = false;
                                att.Mode = IndicatorMode.Buy;
                                position.PositionSettings.SellRSITriggered = false;
                                position.PositionSettings.SellRSITrigger = false;
                            }
                        }
                    }

                    else if (att.STMode == IndicatorMode.Sell)
                    {
                        if (position.PositionSettings.SellRSITriggered && position.PositionSettings.SellRSITrigger)
                        {
                            position.PositionSettings.SellRSITrigger = false;
                            position.PositionSettings.SellRSITriggered = false;
                        }
                        if (position.PositionSettings.SellMACDTriggered && position.PositionSettings.SellMACDTrigger)
                        {
                            position.PositionSettings.SellMACDTrigger = false;
                            position.PositionSettings.SellMACDTriggered = false;
                        }
                        ///if (att.IsNewSFSignal && att.RSIMode == IndicatorMode.Sell && att.MACDMode == IndicatorMode.Sell  && Math.Abs(decimal.Subtract(att.MACD, att.SignalLine)) >= macdNoise)
                        if (att.IsNewSFSignal && att.RSIMode == IndicatorMode.Sell && att.MACDMode == IndicatorMode.Sell && att.MFIEMA1 > att.MFI)// && Math.Abs(decimal.Subtract(att.MACD, att.SignalLine)) >= macdNoise)

                        {
                            att.Mode = IndicatorMode.Sell;
                        }
                        //if (att.IsNewSFSignal && att.RSIMode == IndicatorMode.Sell && att.MACDMode == IndicatorMode.Sell && att.AdxMode == IndicatorMode.Sell)
                        else if (position.PositionSettings.Rule6 && att.STMode == IndicatorMode.Sell && att.RSIMode == IndicatorMode.Sell && att.MACDMode == IndicatorMode.Sell
                            && (fAttributes[i - 1].AdxMode == IndicatorMode.Buy || fAttributes[i - 2].AdxMode == IndicatorMode.Buy) && att.AdxMode == IndicatorMode.Sell
                            ///&& Math.Abs(decimal.Subtract(att.PostiveDIn, att.NegativeDIn)) >= 7
                            && att.MFI < att.MFIEMA1)
                        {

                            att.Mode = IndicatorMode.Sell;
                        }

                        ///else if (position.PositionSettings.Rule7 && att.IsNewSFSignal && att.STMode == IndicatorMode.Sell && att.RSIMode == IndicatorMode.Sell && att.MACDMode == IndicatorMode.Sell && Math.Abs(decimal.Subtract(att.MACD, att.SignalLine)) >= macdNoise && att.MFI < att.MFIEMA1)
                        else if (position.PositionSettings.Rule7 && att.IsNewSFSignal && att.STMode == IndicatorMode.Sell && att.RSIMode == IndicatorMode.Sell && att.MACDMode == IndicatorMode.Sell && att.MFI < att.MFIEMA1)
                        {
                            att.Mode = IndicatorMode.Sell;
                        }
                        //else if (fAttributes[i - 1].Mode == IndicatorMode.Buy && fAttributes[i - 1].STMode == IndicatorMode.Sell && att.MACDMode == IndicatorMode.Sell)
                        ////
                        //{

                        //}
                        else if (position.PositionSettings.Rule3 && att.STMode == IndicatorMode.Sell && att.RSIMode == IndicatorMode.Buy && att.MACDMode == IndicatorMode.Buy
                            && Math.Abs(decimal.Subtract(att.MACD, att.SignalLine)) > 3 && att.MFI > att.MFIEMA1)// && att.Adx < att.NegativeDIn)
                        {
                            att.EarlyBird = true;
                            att.Mode = IndicatorMode.Buy;
                            //if (att.NegativeDIn > att.PostiveDIn && att.Adx > att.NegativeDIn)
                            //{
                            //    att.Mode = IndicatorMode.Sell;
                            //}
                            //else
                            //{
                            //    att.EarlyBird = true;
                            //    att.Mode = IndicatorMode.Buy;
                            //}
                        }
                        else if (position.PositionSettings.Rule3 && fAttributes[i - 1].EarlyBird == true && att.STMode == IndicatorMode.Sell && att.RSIMode == IndicatorMode.Buy && att.MACDMode == IndicatorMode.Buy && att.MFI > att.MFIEMA1)
                        {
                            att.EarlyBird = true;
                            att.Mode = IndicatorMode.Buy;
                        }
                        else if (position.PositionSettings.Rule10 && att.STMode == IndicatorMode.Sell && att.RSIMode == IndicatorMode.Sell && att.MACDMode == IndicatorMode.Sell && Math.Abs(decimal.Subtract(att.MACD, att.SignalLine)) >= macdNoise && att.MFI < att.MFIEMA1)
                        {
                            att.Mode = IndicatorMode.Sell;
                        }
                        //new
                        else if (position.PositionSettings.Rule10 && att.STMode == IndicatorMode.Sell && att.RSIMode == IndicatorMode.Sell
                            && att.AdxMode == IndicatorMode.Sell && (fAttributes[i - 1].AdxMode == IndicatorMode.Buy || fAttributes[i - 2].AdxMode == IndicatorMode.Buy)
                            && att.MACDMode == IndicatorMode.Buy
                            && Math.Abs(decimal.Subtract(fAttributes[i - 1].MACD, fAttributes[i - 1].SignalLine)) >= macdNoise && att.MFI < att.MFIEMA1)
                        {
                            att.Mode = IndicatorMode.Sell;
                        }

                        else
                        {
                            att.Mode = fAttributes[i - 1].Mode;// IndicatorMode.Buy;
                        }


                        if (true)
                        {
                            if (position.PositionSettings.SellMFITriggered && att.Mode == IndicatorMode.Buy)
                            {
                                att.Mode = IndicatorMode.Sell;
                            }
                            else if (position.PositionSettings.SellMFITriggered && att.Mode == IndicatorMode.Sell)
                            {
                                position.PositionSettings.SellMFITriggered = false;
                            }


                            if (true)
                            {
                                if (att.MFI > fAttributes[i - 1].MFI && att.MACD > att.SignalLine && !position.PositionSettings.BuyMFITriggered
                                    && position.PositionSettings.BuyMFITrigger && (att.Adx < att.NegativeDIn || att.AdxMode == IndicatorMode.Buy))
                                {
                                    if (att.Adx < att.NegativeDIn)
                                    {

                                    }
                                    if (att.AdxMode == IndicatorMode.Buy)
                                    {

                                    }
                                    att.Mode = IndicatorMode.Buy;
                                    position.PositionSettings.BuyMFITriggered = true;
                                    position.PositionSettings.BuyMFITrigger = false;
                                }
                                if (att.MFI < 10 && !position.PositionSettings.BuyMFITrigger && !position.PositionSettings.BuyMFITriggered)
                                {
                                    position.PositionSettings.BuyMFITrigger = true;
                                }

                                if (position.PositionSettings.BuyMFITriggered)
                                {
                                    att.Mode = IndicatorMode.Buy;
                                }
                            }


                            //Reset MFI
                            if (true && position.PositionSettings.BuyMFITriggered && //att.MFI > att.MFIEMA1 &&
                                att.SignalLine > att.MACD
                                && (Math.Abs(decimal.Subtract(att.MACD, att.SignalLine)) > 2
                             )
                                //&& att.rs
                                //&& att.MFI < 80
                                && (fAttributes[i - 1].MACDMode == IndicatorMode.Buy || fAttributes[i - 2].MACDMode == IndicatorMode.Buy)
                                && att.AdxMode == IndicatorMode.Sell)
                            {
                                position.PositionSettings.BuyMFITriggered = false;
                                att.Mode = IndicatorMode.Sell;
                                position.PositionSettings.BuyMFITriggered = false;
                                position.PositionSettings.BuyMFITrigger = false;
                            }

                            if (!position.PositionSettings.BuyRSITriggered && position.PositionSettings.BuyRSITrigger && position.PositionSettings.Rule5
                                && att.RSIEMA1 > att.RSIEMA2 && att.MACD > att.SignalLine)
                            {
                                att.Mode = IndicatorMode.Buy;
                                position.PositionSettings.BuyRSITriggered = true;
                            }

                            if (!position.PositionSettings.BuyRSITrigger && position.PositionSettings.Rule5 && att.STMode == IndicatorMode.Sell && att.RSI < 20 && att.Mode == IndicatorMode.Sell)
                            {
                                position.PositionSettings.BuyRSITrigger = true;
                            }

                            if (position.PositionSettings.BuyRSITriggered && att.MACD > att.SignalLine)
                            {
                                att.Mode = IndicatorMode.Buy;
                            }
                        }
                    }

                    //if (position.PositionSettings.SellMFITriggered && att.MFI > fAttributes[i - 1].MFIEMA1 && att.MACD > fAttributes[i - 1].MACD && att.Mode == IndicatorMode.Buy)
                    //{
                    //    att.Mode = IndicatorMode.Buy;
                    //    position.PositionSettings.SellMFITriggered = false;
                    //    position.PositionSettings.MFIOff = false;
                    //}

                    //if (position.PositionSettings.SellMFITriggered && att.MFI > fAttributes[i - 1].MFIEMA1)// && att.MACD > fAttributes[i - 1].MACD)
                    //{
                    //    if (att.Close == 2522.25M)
                    //    {

                    //    }
                    //    position.PositionSettings.MFIOff = true;
                    //    att.Mode = IndicatorMode.Buy;
                    //}

                    //if (position.PositionSettings.MFIOff == true && position.PositionSettings.SellMFITriggered && att.Mode == IndicatorMode.Sell)
                    //{

                    //}
                    //if (position.PositionSettings.SellMFITriggered && att.MFI < fAttributes[i - 1].MFI && att.MACD < fAttributes[i - 1].MACD && att.Mode == IndicatorMode.Buy)
                    //{
                    //    att.Mode = IndicatorMode.Sell;
                    //}

                    if (att.STMode == IndicatorMode.Sell && position.PositionSettings.WFFMode != IndicatorMode.None)
                    {
                        position.PositionSettings.WFFMode = IndicatorMode.None;
                    }

                    if (position.PositionSettings.WFFMode != IndicatorMode.None)
                    {
                        att.Mode = IndicatorMode.Sell;
                    }

                    endPrice = att.Close;
                    if (att.Mode != fAttributes[i - 1].Mode)
                    {

                        if (att.MFI > 80 && att.Mode == IndicatorMode.Buy)
                        {
                            position.PositionSettings.WFFMode = att.Mode;
                            att.Mode = IndicatorMode.Sell;
                        }
                        if (att.Adx < 25 && !position.PositionSettings.BuyMFITriggered)
                        {

                        }
                        if (att.Adx < att.PostiveDIn && att.Adx < att.NegativeDIn && !position.PositionSettings.BuyMFITriggered)
                        {
                            // att.Mode==fAttributes[i-1].Mode
                        }
                        if ((startPrice < endPrice) && att.Mode == IndicatorMode.Buy)
                        {
                            isLoss = true;
                        }
                        if ((startPrice > endPrice) && att.Mode == IndicatorMode.Sell)
                        {
                            isLoss = true;
                        }
                        startPrice = att.Close;
                        startDateTime = att.TimeStamp;
                    }


                }
            }
        }

        private static void Analysis_Risk1(Position position, int days, decimal macdNoise)
        {
            List<Attributes> fAttributes = position.PositionAttributes.ToList();
            decimal startPrice = 0, endPrice = 0; DateTime startDateTime = new DateTime();
            bool isLoss = false;

            foreach (Attributes att in fAttributes)
            {
                int i = fAttributes.IndexOf(att);
                if (i > 28)
                {
                    //Test
                    if (att.TimeStamp > GetIndianDateTime().AddDays(-1))
                    {

                    }
                    //
                    if (att.Close == 695.80M)
                    {

                    }
                    if (att.TimeStamp == Convert.ToDateTime("2019-01-30 22:45:00.000"))
                    {

                    }

                    //if (att.Adx <= 20)
                    //{
                    //    att.Mode = fAttributes[i - 1].Mode; //IndicatorMode.Sell;
                    //    continue;
                    //}
                    //MACD Convergence
                    //Decimal ConvergencePct = 0;
                    //Decimal Convergence = (
                    //    Math.Abs(CurrentTick.MACD - CurrentTick.MACDSignal) +
                    //    Math.Abs(PreviousTick.MACD - PreviousTick.MACDSignal)
                    //);

                    //if (Convergence > 0.001M)
                    //{
                    //    ConvergencePct = Math.Abs(Convergence / CurrentTick.MACD);
                    //}

                    if (fAttributes[i - 1].Mode == IndicatorMode.None)
                    {


                    }
                    if (fAttributes[i].TimeStamp >= Convert.ToDateTime("06-02-2018  09:45:00"))
                    {

                    }
                    if (fAttributes[i].Close == 2792)
                    {

                    }
                    decimal curdiff = Math.Abs(Math.Abs(fAttributes[i].MACD) - Math.Abs(fAttributes[i].SignalLine));
                    decimal prevdiff = Math.Abs(Math.Abs(fAttributes[i - 1].MACD) - Math.Abs(fAttributes[i - 1].SignalLine));

                    decimal currCon = curdiff / Math.Abs(fAttributes[i].MACD);
                    decimal prevCon = prevdiff / Math.Abs(fAttributes[i - 1].MACD);



                    //if (position.PositionSettings.Rule2 && att.RSIMode == IndicatorMode.Buy
                    //        && att.MACDMode == IndicatorMode.Buy
                    //        //&& Math.Abs(decimal.Subtract(Math.Abs(att.MACD), Math.Abs(att.SignalLine))) >= macdNoise
                    //        && att.MFI > att.MFIEMA1
                    //        //&& att.Close > position.PositionSettings.maxLeadingSpan 
                    //        && att.PostiveDIn > att.NegativeDIn && att.STMode == IndicatorMode.Sell)
                    //{
                    //    position.PositionSettings.BuyonShortRule1 = true;
                    //    att.Mode = IndicatorMode.Buy;
                    //    continue;
                    //}

                    //if (att.MFI > att.MFIEMA1 && att.MFIEMA1 > 0)
                    //{
                    //    position.PositionSettings.MFIBuyCount += 1;
                    //}
                    //else
                    //{
                    //    if (position.PositionSettings.MFIBuyCount > 10)
                    //    {

                    //    }
                    //    position.PositionSettings.MFIBuyCount = 0;
                    //}

                    if (att.STMode == IndicatorMode.Buy)
                    {
                        if (position.PositionSettings.BuyRSITriggered || position.PositionSettings.BuyRSITrigger)
                        {
                            position.PositionSettings.BuyRSITrigger = false;
                            position.PositionSettings.BuyRSITriggered = false;
                        }
                        if (position.PositionSettings.BuyMFITrigger || position.PositionSettings.BuyMFITriggered)
                        {
                            position.PositionSettings.BuyMFITrigger = false;
                            position.PositionSettings.BuyMFITriggered = false;
                        }

                        if (att.STMode == IndicatorMode.Buy //&& att.RSIMode == IndicatorMode.Buy 
                            && att.MACDMode == IndicatorMode.Buy && att.AdxMode == IndicatorMode.Buy
                            && (fAttributes[i - 1].AdxMode == IndicatorMode.Sell || fAttributes[i - 2].AdxMode == IndicatorMode.Sell)
                            && Math.Abs(decimal.Subtract(att.PostiveDIn, att.NegativeDIn)) >= 4
                            && att.MFI > att.MFIEMA1)
                        {
                            att.Mode = IndicatorMode.Buy;
                        }

                        else if (position.PositionSettings.Rule2 && att.IsNewSFSignal && att.STMode == IndicatorMode.Buy && att.RSIMode == IndicatorMode.Buy
                            && att.MACDMode == IndicatorMode.Buy
                            ///&& Math.Abs(decimal.Subtract(Math.Abs(att.MACD), Math.Abs(att.SignalLine))) >= macdNoise 
                            && att.MFI > att.MFIEMA1)
                        {
                            att.Mode = IndicatorMode.Buy;
                        }

                        //Sell 
                        else if (position.PositionSettings.Rule3 && att.STMode == IndicatorMode.Buy && att.RSIMode == IndicatorMode.Sell
                            && att.MACDMode == IndicatorMode.Sell
                            && Math.Abs(decimal.Subtract(att.MACD, att.SignalLine)) > 7 && att.MFI < att.MFIEMA1)
                        {
                            att.EarlyBird = true;
                            att.Mode = IndicatorMode.Sell;
                        }
                        else if (position.PositionSettings.Rule3 && att.STMode == IndicatorMode.Buy && fAttributes[i - 1].EarlyBird == true
                            && att.RSIMode == IndicatorMode.Sell && att.MACDMode == IndicatorMode.Sell && att.MFI < att.MFIEMA1)// && att.Adx < att.PostiveDIn)
                        {
                            att.EarlyBird = true;
                            att.Mode = IndicatorMode.Sell;
                        }
                        //End Sell

                        else if (position.PositionSettings.Rule5 && att.STMode == IndicatorMode.Buy && att.RSIMode == IndicatorMode.Buy && att.MACDMode == IndicatorMode.Buy
                            && Math.Abs(decimal.Subtract(att.MACD, att.SignalLine)) >= macdNoise
                            && att.MFI > att.MFIEMA1)
                        {
                            att.Mode = IndicatorMode.Buy;
                        }
                        //else if (position.PositionSettings.Rule5 && att.STMode == IndicatorMode.Buy && att.RSIMode == IndicatorMode.Buy && att.MACDMode == IndicatorMode.Buy
                        //   && (Math.Abs(decimal.Subtract(att.MACD, att.SignalLine)) >= macdNoise || att.AdxMode == IndicatorMode.Buy
                        //   && att.MFI > att.MFIEMA1))
                        //{

                        //    att.Mode = IndicatorMode.Buy;
                        //}
                        else
                            att.Mode = fAttributes[i - 1].Mode;


                        if (position.PositionSettings.BuyMFITriggered && att.Mode == IndicatorMode.Sell)
                        {
                            att.Mode = IndicatorMode.Buy;
                        }
                        else if (position.PositionSettings.BuyMFITriggered && att.Mode == IndicatorMode.Buy)
                        {
                            position.PositionSettings.BuyMFITriggered = false;
                        }

                        if (true)
                        {
                            if (true)
                            {
                                if (att.MFI > 85 && !position.PositionSettings.SellMFITrigger && !position.PositionSettings.SellMFITriggered)
                                {
                                    position.PositionSettings.SellMFITrigger = true;
                                }

                                if (position.PositionSettings.SellMFITriggered && att.MFI > fAttributes[i - 1].MFI && att.MACD > fAttributes[i - 1].MACD)
                                {

                                }
                                if (true //&& att.MFI < fAttributes[i - 1].MFI 
                                         //&& att.MFI < 90
                                    && att.MFIEMA1 > att.MFI
                                    //&& att.MACD < fAttributes[i - 1].MACD
                                    //&& att.MFI < 80
                                    //&& att.MACD < fAttributes[i - 1].SignalLine
                                    //&& att.Adx < fAttributes[i - 1].Adx
                                    //&& att.RSI < fAttributes[i - 1].RSI // && att.Adx < att.PostiveDIn //&& att.MACD < fAttributes[i - 1].MACD
                                    && !position.PositionSettings.SellMFITriggered && position.PositionSettings.SellMFITrigger)
                                {
                                    if (fAttributes[i - 1].MFI > att.MFI)
                                    {

                                    }
                                    att.Mode = IndicatorMode.Sell;
                                    position.PositionSettings.SellMFITriggered = true;
                                    position.PositionSettings.SellMFITrigger = false;
                                }

                                if (position.PositionSettings.SellMFITriggered)
                                {
                                    att.Mode = IndicatorMode.Sell;
                                    //att.Mode = IndicatorMode.SellNWait;
                                }
                            }

                            if (!position.PositionSettings.SellRSITriggered && position.PositionSettings.SellRSITrigger && position.PositionSettings.Rule5
                                && att.RSIEMA1 < att.RSIEMA2 && att.MACD < att.SignalLine)
                            {
                                att.Mode = IndicatorMode.Sell;
                                position.PositionSettings.SellRSITriggered = true;
                            }
                            if (!position.PositionSettings.SellRSITrigger && position.PositionSettings.Rule5 && att.STMode == IndicatorMode.Buy && att.RSI > 80 && att.Mode == IndicatorMode.Buy)
                            {
                                position.PositionSettings.SellRSITrigger = true;
                            }

                            if (position.PositionSettings.SellRSITriggered && att.MACD > att.SignalLine)
                            {
                                position.PositionSettings.SellRSITriggered = true;
                            }
                            if (position.PositionSettings.SellRSITriggered)
                            {
                                att.Mode = IndicatorMode.Sell;
                            }

                            //Reset MFI
                            if (true && position.PositionSettings.SellMFITriggered && att.MFI > att.MFIEMA1 &&
                                att.MACD > att.SignalLine
                                //&& 
                                //(Math.Abs(decimal.Subtract(att.MACD, att.SignalLine)) > 0
                                //|| att.MFI > att.MFIEMA1)
                                //&& att.ConverstionLine > att.BaseLine
                                && (fAttributes[i - 1].MACDMode == IndicatorMode.Sell || fAttributes[i - 2].MACDMode == IndicatorMode.Sell)
                                && att.AdxMode == IndicatorMode.Buy)
                            {
                                position.PositionSettings.SellMFITriggered = false;
                                att.Mode = IndicatorMode.Buy;
                                position.PositionSettings.SellRSITriggered = false;
                                position.PositionSettings.SellRSITrigger = false;
                            }

                            if (att.MACD > att.SignalLine && att.MFI > att.MFIEMA1 && att.MFI < 70 && position.PositionSettings.SellMFITriggered)
                            {
                                position.PositionSettings.SellMFITriggered = false;
                                att.Mode = IndicatorMode.Buy;
                                position.PositionSettings.SellRSITriggered = false;
                                position.PositionSettings.SellRSITrigger = false;
                            }
                        }
                    }

                    else if (att.STMode == IndicatorMode.Sell)
                    {
                        if (position.PositionSettings.SellRSITriggered && position.PositionSettings.SellRSITrigger)
                        {
                            position.PositionSettings.SellRSITrigger = false;
                            position.PositionSettings.SellRSITriggered = false;
                        }
                        if (position.PositionSettings.SellMACDTriggered && position.PositionSettings.SellMACDTrigger)
                        {
                            position.PositionSettings.SellMACDTrigger = false;
                            position.PositionSettings.SellMACDTriggered = false;
                        }
                        ///if (att.IsNewSFSignal && att.RSIMode == IndicatorMode.Sell && att.MACDMode == IndicatorMode.Sell  && Math.Abs(decimal.Subtract(att.MACD, att.SignalLine)) >= macdNoise)
                        if (att.IsNewSFSignal && att.RSIMode == IndicatorMode.Sell && att.MACDMode == IndicatorMode.Sell && att.MFIEMA1 > att.MFI)// && Math.Abs(decimal.Subtract(att.MACD, att.SignalLine)) >= macdNoise)

                        {
                            att.Mode = IndicatorMode.Sell;
                        }
                        //if (att.IsNewSFSignal && att.RSIMode == IndicatorMode.Sell && att.MACDMode == IndicatorMode.Sell && att.AdxMode == IndicatorMode.Sell)
                        else if (position.PositionSettings.Rule6 && att.STMode == IndicatorMode.Sell && att.RSIMode == IndicatorMode.Sell && att.MACDMode == IndicatorMode.Sell
                            && (fAttributes[i - 1].AdxMode == IndicatorMode.Buy || fAttributes[i - 2].AdxMode == IndicatorMode.Buy) && att.AdxMode == IndicatorMode.Sell
                            ///&& Math.Abs(decimal.Subtract(att.PostiveDIn, att.NegativeDIn)) >= 7
                            && att.MFI < att.MFIEMA1)
                        {

                            att.Mode = IndicatorMode.Sell;
                        }

                        ///else if (position.PositionSettings.Rule7 && att.IsNewSFSignal && att.STMode == IndicatorMode.Sell && att.RSIMode == IndicatorMode.Sell && att.MACDMode == IndicatorMode.Sell && Math.Abs(decimal.Subtract(att.MACD, att.SignalLine)) >= macdNoise && att.MFI < att.MFIEMA1)
                        else if (position.PositionSettings.Rule7 && att.IsNewSFSignal && att.STMode == IndicatorMode.Sell && att.RSIMode == IndicatorMode.Sell && att.MACDMode == IndicatorMode.Sell && att.MFI < att.MFIEMA1)
                        {
                            att.Mode = IndicatorMode.Sell;
                        }
                        //else if (fAttributes[i - 1].Mode == IndicatorMode.Buy && fAttributes[i - 1].STMode == IndicatorMode.Sell && att.MACDMode == IndicatorMode.Sell)
                        ////
                        //{

                        //}
                        else if (position.PositionSettings.Rule3 && att.STMode == IndicatorMode.Sell && att.RSIMode == IndicatorMode.Buy && att.MACDMode == IndicatorMode.Buy
                            && Math.Abs(decimal.Subtract(att.MACD, att.SignalLine)) > 3 && att.MFI > att.MFIEMA1)// && att.Adx < att.NegativeDIn)
                        {
                            att.EarlyBird = true;
                            att.Mode = IndicatorMode.Buy;
                            //if (att.NegativeDIn > att.PostiveDIn && att.Adx > att.NegativeDIn)
                            //{
                            //    att.Mode = IndicatorMode.Sell;
                            //}
                            //else
                            //{
                            //    att.EarlyBird = true;
                            //    att.Mode = IndicatorMode.Buy;
                            //}
                        }
                        else if (position.PositionSettings.Rule3 && fAttributes[i - 1].EarlyBird == true && att.STMode == IndicatorMode.Sell && att.RSIMode == IndicatorMode.Buy && att.MACDMode == IndicatorMode.Buy && att.MFI > att.MFIEMA1)
                        {
                            att.EarlyBird = true;
                            att.Mode = IndicatorMode.Buy;
                        }
                        else if (position.PositionSettings.Rule10 && att.STMode == IndicatorMode.Sell && att.RSIMode == IndicatorMode.Sell && att.MACDMode == IndicatorMode.Sell && Math.Abs(decimal.Subtract(att.MACD, att.SignalLine)) >= macdNoise && att.MFI < att.MFIEMA1)
                        {
                            att.Mode = IndicatorMode.Sell;
                        }
                        //new
                        else if (position.PositionSettings.Rule10 && att.STMode == IndicatorMode.Sell && att.RSIMode == IndicatorMode.Sell
                            && att.AdxMode == IndicatorMode.Sell && (fAttributes[i - 1].AdxMode == IndicatorMode.Buy || fAttributes[i - 2].AdxMode == IndicatorMode.Buy)
                            && att.MACDMode == IndicatorMode.Buy
                            && Math.Abs(decimal.Subtract(fAttributes[i - 1].MACD, fAttributes[i - 1].SignalLine)) >= macdNoise && att.MFI < att.MFIEMA1)
                        {
                            att.Mode = IndicatorMode.Sell;
                        }

                        else
                        {
                            att.Mode = fAttributes[i - 1].Mode;// IndicatorMode.Buy;
                        }


                        if (true)
                        {
                            if (position.PositionSettings.SellMFITriggered && att.Mode == IndicatorMode.Buy)
                            {
                                att.Mode = IndicatorMode.Sell;
                            }
                            else if (position.PositionSettings.SellMFITriggered && att.Mode == IndicatorMode.Sell)
                            {
                                position.PositionSettings.SellMFITriggered = false;
                            }


                            if (true)
                            {
                                if (
                                    //att.MFI > fAttributes[i - 1].MFI 
                                    att.MFI > att.MFIEMA1
                                    //&& att.MACD > att.SignalLine
                                    && !position.PositionSettings.BuyMFITriggered
                                    && position.PositionSettings.BuyMFITrigger
                                    //&& (att.Adx < att.NegativeDIn || att.AdxMode == IndicatorMode.Buy)
                                    )
                                {
                                    if (att.Adx < att.NegativeDIn)
                                    {

                                    }
                                    if (att.AdxMode == IndicatorMode.Buy)
                                    {

                                    }
                                    att.Mode = IndicatorMode.Buy;
                                    position.PositionSettings.BuyMFITriggered = true;
                                    position.PositionSettings.BuyMFITrigger = false;
                                }
                                if (att.MFI < 10 && !position.PositionSettings.BuyMFITrigger && !position.PositionSettings.BuyMFITriggered)
                                {
                                    position.PositionSettings.BuyMFITrigger = true;
                                }

                                if (position.PositionSettings.BuyMFITriggered)
                                {
                                    att.Mode = IndicatorMode.Buy;
                                }
                            }


                            //Reset MFI
                            if (true && position.PositionSettings.BuyMFITriggered && //att.MFI > att.MFIEMA1 &&
                                att.SignalLine > att.MACD
                                && (Math.Abs(decimal.Subtract(att.MACD, att.SignalLine)) > 2
                             )
                                //&& att.rs
                                //&& att.MFI < 80
                                && (fAttributes[i - 1].MACDMode == IndicatorMode.Buy || fAttributes[i - 2].MACDMode == IndicatorMode.Buy)
                                && att.AdxMode == IndicatorMode.Sell)
                            {
                                position.PositionSettings.BuyMFITriggered = false;
                                att.Mode = IndicatorMode.Sell;
                                position.PositionSettings.BuyMFITriggered = false;
                                position.PositionSettings.BuyMFITrigger = false;
                            }

                            if (!position.PositionSettings.BuyRSITriggered && position.PositionSettings.BuyRSITrigger && position.PositionSettings.Rule5
                                && att.RSIEMA1 > att.RSIEMA2 && att.MACD > att.SignalLine)
                            {
                                att.Mode = IndicatorMode.Buy;
                                position.PositionSettings.BuyRSITriggered = true;
                            }

                            if (!position.PositionSettings.BuyRSITrigger && position.PositionSettings.Rule5 && att.STMode == IndicatorMode.Sell && att.RSI < 20 && att.Mode == IndicatorMode.Sell)
                            {
                                position.PositionSettings.BuyRSITrigger = true;
                            }

                            if (position.PositionSettings.BuyRSITriggered && att.MACD > att.SignalLine)
                            {
                                att.Mode = IndicatorMode.Buy;
                            }
                        }
                    }

                    
                    if (att.STMode == IndicatorMode.Sell && position.PositionSettings.WFFMode != IndicatorMode.None)
                    {
                        position.PositionSettings.WFFMode = IndicatorMode.None;
                    }

                    if (position.PositionSettings.WFFMode != IndicatorMode.None)
                    {
                        att.Mode = IndicatorMode.Sell;
                    }

                    endPrice = att.Close;
                    if (att.Mode != fAttributes[i - 1].Mode)
                    {

                        if (att.MFI > 80 && att.Mode == IndicatorMode.Buy)
                        {
                            position.PositionSettings.WFFMode = att.Mode;
                            att.Mode = IndicatorMode.Sell;
                        }
                        if (att.Adx < 25 && !position.PositionSettings.BuyMFITriggered)
                        {

                        }
                        if (att.Adx < att.PostiveDIn && att.Adx < att.NegativeDIn && !position.PositionSettings.BuyMFITriggered)
                        {
                            // att.Mode==fAttributes[i-1].Mode
                        }
                        if ((startPrice < endPrice) && att.Mode == IndicatorMode.Buy)
                        {
                            isLoss = true;
                        }
                        if ((startPrice > endPrice) && att.Mode == IndicatorMode.Sell)
                        {
                            isLoss = true;
                        }
                        startPrice = att.Close;
                        startDateTime = att.TimeStamp;
                    }


                }
            }
        }

        private static void Analysis_v5(Position position, int days, decimal macdNoise)
        {
            List<Attributes> fAttributes = position.PositionAttributes.ToList();
            decimal startPrice = 0, endPrice = 0; DateTime startDateTime = new DateTime();
            bool isLoss = false;

            foreach (Attributes att in fAttributes)
            {
                int i = fAttributes.IndexOf(att);
                if (i > 28)
                {
                    //Test
                    if (att.TimeStamp > GetIndianDateTime().AddDays(-1))
                    {

                    }
                    //
                    if (att.Close == 695.80M)
                    {

                    }
                    if (att.TimeStamp == Convert.ToDateTime("2019-01-30 22:45:00.000"))
                    {

                    }

                    //if (att.Adx <= 20)
                    //{
                    //    att.Mode = fAttributes[i - 1].Mode; //IndicatorMode.Sell;
                    //    continue;
                    //}
                    //MACD Convergence
                    //Decimal ConvergencePct = 0;
                    //Decimal Convergence = (
                    //    Math.Abs(CurrentTick.MACD - CurrentTick.MACDSignal) +
                    //    Math.Abs(PreviousTick.MACD - PreviousTick.MACDSignal)
                    //);

                    //if (Convergence > 0.001M)
                    //{
                    //    ConvergencePct = Math.Abs(Convergence / CurrentTick.MACD);
                    //}

                    if (fAttributes[i - 1].Mode == IndicatorMode.None)
                    {


                    }
                    if (fAttributes[i].TimeStamp >= Convert.ToDateTime("06-02-2018  09:45:00"))
                    {

                    }
                    if (fAttributes[i].Close == 2792)
                    {

                    }
                    decimal curdiff = Math.Abs(Math.Abs(fAttributes[i].MACD) - Math.Abs(fAttributes[i].SignalLine));
                    decimal prevdiff = Math.Abs(Math.Abs(fAttributes[i - 1].MACD) - Math.Abs(fAttributes[i - 1].SignalLine));

                    decimal currCon = curdiff / Math.Abs(fAttributes[i].MACD);
                    decimal prevCon = prevdiff / Math.Abs(fAttributes[i - 1].MACD);

                    if (att.STMode == IndicatorMode.Buy)
                    {
                        if (position.PositionSettings.BuyRSITriggered || position.PositionSettings.BuyRSITrigger)
                        {
                            position.PositionSettings.BuyRSITrigger = false;
                            position.PositionSettings.BuyRSITriggered = false;
                        }
                        if (position.PositionSettings.BuyMFITrigger || position.PositionSettings.BuyMFITriggered)
                        {
                            position.PositionSettings.BuyMFITrigger = false;
                            position.PositionSettings.BuyMFITriggered = false;
                        }

                        if (position.PositionSettings.Rule1 && att.STMode == IndicatorMode.Buy && att.RSIMode == IndicatorMode.Buy && att.MACDMode == IndicatorMode.Buy && att.AdxMode == IndicatorMode.Buy
                            && (fAttributes[i - 1].AdxMode == IndicatorMode.Sell || fAttributes[i - 2].AdxMode == IndicatorMode.Sell)
                            && Math.Abs(decimal.Subtract(att.PostiveDIn, att.NegativeDIn)) >= 4
                            && att.MFI > att.MFIEMA1)
                        {
                            att.Mode = IndicatorMode.Buy;
                        }

                        else if (position.PositionSettings.Rule2 && att.IsNewSFSignal && att.STMode == IndicatorMode.Buy
                            && att.RSIMode == IndicatorMode.Buy && att.MACDMode == IndicatorMode.Buy
                            ///&& Math.Abs(decimal.Subtract(Math.Abs(att.MACD), Math.Abs(att.SignalLine))) >= macdNoise 
                            && att.MFI > att.MFIEMA1)
                        {
                            att.Mode = IndicatorMode.Buy;
                        }

                        else if (position.PositionSettings.Rule3 && att.STMode == IndicatorMode.Buy && att.RSIMode == IndicatorMode.Sell && att.MACDMode == IndicatorMode.Sell && Math.Abs(decimal.Subtract(att.MACD, att.SignalLine)) > 7 && att.MFI < att.MFIEMA1)
                        {
                            att.EarlyBird = true;
                            att.Mode = IndicatorMode.Sell;
                        }
                        else if (position.PositionSettings.Rule3 && att.STMode == IndicatorMode.Buy && fAttributes[i - 1].EarlyBird == true
                            && att.RSIMode == IndicatorMode.Sell && att.MACDMode == IndicatorMode.Sell && att.MFI < att.MFIEMA1)// && att.Adx < att.PostiveDIn)
                        {
                            att.EarlyBird = true;
                            att.Mode = IndicatorMode.Sell;
                        }
                        else if (position.PositionSettings.Rule5 && att.STMode == IndicatorMode.Buy && att.RSIMode == IndicatorMode.Buy && att.MACDMode == IndicatorMode.Buy
                            && Math.Abs(decimal.Subtract(att.MACD, att.SignalLine)) >= macdNoise
                            && att.MFI > att.MFIEMA1)
                        {

                            att.Mode = IndicatorMode.Buy;
                        }
                        //else if (position.PositionSettings.Rule5 && att.STMode == IndicatorMode.Buy && att.RSIMode == IndicatorMode.Buy && att.MACDMode == IndicatorMode.Buy
                        //   && (Math.Abs(decimal.Subtract(att.MACD, att.SignalLine)) >= macdNoise || att.AdxMode == IndicatorMode.Buy
                        //   && att.MFI > att.MFIEMA1))
                        //{

                        //    att.Mode = IndicatorMode.Buy;
                        //}
                        else
                            att.Mode = fAttributes[i - 1].Mode;


                        if (position.PositionSettings.BuyMFITriggered && att.Mode == IndicatorMode.Sell)
                        {
                            att.Mode = IndicatorMode.Buy;
                        }
                        else if (position.PositionSettings.BuyMFITriggered && att.Mode == IndicatorMode.Buy)
                        {
                            position.PositionSettings.BuyMFITriggered = false;
                        }

                        if (true)
                        {
                            if (true)
                            {
                                if (att.MFI > 85 && !position.PositionSettings.SellMFITrigger && !position.PositionSettings.SellMFITriggered)
                                {
                                    position.PositionSettings.SellMFITrigger = true;
                                }

                                if (true //&& att.MFI < fAttributes[i - 1].MFI 
                                         //&& att.MFI < 90
                                    && att.MFIEMA1 > att.MFI
                                    //&& att.MACD < fAttributes[i - 1].MACD 
                                    //&& att.Adx < fAttributes[i - 1].Adx
                                    //&& att.RSI < fAttributes[i - 1].RSI // && att.Adx < att.PostiveDIn //&& att.MACD < fAttributes[i - 1].MACD
                                    && !position.PositionSettings.SellMFITriggered && position.PositionSettings.SellMFITrigger)
                                {
                                    if (fAttributes[i - 1].MFI > att.MFI)
                                    {

                                    }
                                    att.Mode = IndicatorMode.Sell;
                                    position.PositionSettings.SellMFITriggered = true;
                                    position.PositionSettings.SellMFITrigger = false;
                                }

                                if (position.PositionSettings.SellMFITriggered)
                                {
                                    att.Mode = IndicatorMode.Sell;
                                    //att.Mode = IndicatorMode.SellNWait;
                                }
                            }

                            if (!position.PositionSettings.SellRSITriggered && position.PositionSettings.SellRSITrigger && position.PositionSettings.Rule5
                                && att.RSIEMA1 < att.RSIEMA2 && att.MACD < att.SignalLine)
                            {
                                att.Mode = IndicatorMode.Sell;
                                position.PositionSettings.SellRSITriggered = true;
                            }
                            if (!position.PositionSettings.SellRSITrigger && position.PositionSettings.Rule5 && att.STMode == IndicatorMode.Buy && att.RSI > 80 && att.Mode == IndicatorMode.Buy)
                            {
                                position.PositionSettings.SellRSITrigger = true;
                            }

                            if (position.PositionSettings.SellRSITriggered && att.MACD > att.SignalLine)
                            {
                                position.PositionSettings.SellRSITriggered = true;
                            }
                            if (position.PositionSettings.SellRSITriggered)
                            {
                                att.Mode = IndicatorMode.Sell;
                            }

                            //Reset MFI
                            if (true && position.PositionSettings.SellMFITriggered && att.MFI > att.MFIEMA1 &&
                                att.MACD > att.SignalLine
                                && (Math.Abs(decimal.Subtract(att.MACD, att.SignalLine)) > 0
                                || att.MFI > att.MFIEMA1)
                                //&& att.rs
                                //&& att.MFI < 80
                                && (fAttributes[i - 1].MACDMode == IndicatorMode.Sell || fAttributes[i - 2].MACDMode == IndicatorMode.Sell)
                                && att.AdxMode == IndicatorMode.Buy)
                            {
                                position.PositionSettings.SellMFITriggered = false;
                                att.Mode = IndicatorMode.Buy;
                                position.PositionSettings.SellRSITriggered = false;
                                position.PositionSettings.SellRSITrigger = false;
                            }

                            if (att.MACD > att.SignalLine && att.MFI > att.MFIEMA1 && att.MFI < 70 && position.PositionSettings.SellMFITriggered)
                            {
                                position.PositionSettings.SellMFITriggered = false;
                                att.Mode = IndicatorMode.Buy;
                                position.PositionSettings.SellRSITriggered = false;
                                position.PositionSettings.SellRSITrigger = false;
                            }
                        }
                    }

                    else if (att.STMode == IndicatorMode.Sell)
                    {
                        if (position.PositionSettings.SellRSITriggered && position.PositionSettings.SellRSITrigger)
                        {
                            position.PositionSettings.SellRSITrigger = false;
                            position.PositionSettings.SellRSITriggered = false;
                        }
                        if (position.PositionSettings.SellMACDTriggered && position.PositionSettings.SellMACDTrigger)
                        {
                            position.PositionSettings.SellMACDTrigger = false;
                            position.PositionSettings.SellMACDTriggered = false;
                        }
                        ///if (att.IsNewSFSignal && att.RSIMode == IndicatorMode.Sell && att.MACDMode == IndicatorMode.Sell  && Math.Abs(decimal.Subtract(att.MACD, att.SignalLine)) >= macdNoise)
                        if (att.IsNewSFSignal && att.RSIMode == IndicatorMode.Sell && att.MACDMode == IndicatorMode.Sell && att.MFIEMA1 > att.MFI)// && Math.Abs(decimal.Subtract(att.MACD, att.SignalLine)) >= macdNoise)

                        {
                            att.Mode = IndicatorMode.Sell;
                        }
                        //if (att.IsNewSFSignal && att.RSIMode == IndicatorMode.Sell && att.MACDMode == IndicatorMode.Sell && att.AdxMode == IndicatorMode.Sell)
                        else if (position.PositionSettings.Rule6 && att.STMode == IndicatorMode.Sell && att.RSIMode == IndicatorMode.Sell && att.MACDMode == IndicatorMode.Sell
                            && (fAttributes[i - 1].AdxMode == IndicatorMode.Buy || fAttributes[i - 2].AdxMode == IndicatorMode.Buy) && att.AdxMode == IndicatorMode.Sell
                            ///&& Math.Abs(decimal.Subtract(att.PostiveDIn, att.NegativeDIn)) >= 7
                            && att.MFI < att.MFIEMA1)
                        {

                            att.Mode = IndicatorMode.Sell;
                        }

                        ///else if (position.PositionSettings.Rule7 && att.IsNewSFSignal && att.STMode == IndicatorMode.Sell && att.RSIMode == IndicatorMode.Sell && att.MACDMode == IndicatorMode.Sell && Math.Abs(decimal.Subtract(att.MACD, att.SignalLine)) >= macdNoise && att.MFI < att.MFIEMA1)
                        else if (position.PositionSettings.Rule7 && att.IsNewSFSignal && att.STMode == IndicatorMode.Sell && att.RSIMode == IndicatorMode.Sell && att.MACDMode == IndicatorMode.Sell && att.MFI < att.MFIEMA1)
                        {
                            att.Mode = IndicatorMode.Sell;
                        }
                        //else if (fAttributes[i - 1].Mode == IndicatorMode.Buy && fAttributes[i - 1].STMode == IndicatorMode.Sell && att.MACDMode == IndicatorMode.Sell)
                        ////
                        //{

                        //}
                        else if (position.PositionSettings.Rule3 && att.STMode == IndicatorMode.Sell && att.RSIMode == IndicatorMode.Buy && att.MACDMode == IndicatorMode.Buy
                            && Math.Abs(decimal.Subtract(att.MACD, att.SignalLine)) > 3 && att.MFI > att.MFIEMA1)// && att.Adx < att.NegativeDIn)
                        {
                            att.EarlyBird = true;
                            att.Mode = IndicatorMode.Buy;
                            //if (att.NegativeDIn > att.PostiveDIn && att.Adx > att.NegativeDIn)
                            //{
                            //    att.Mode = IndicatorMode.Sell;
                            //}
                            //else
                            //{
                            //    att.EarlyBird = true;
                            //    att.Mode = IndicatorMode.Buy;
                            //}
                        }
                        else if (position.PositionSettings.Rule3 && fAttributes[i - 1].EarlyBird == true && att.STMode == IndicatorMode.Sell && att.RSIMode == IndicatorMode.Buy && att.MACDMode == IndicatorMode.Buy && att.MFI > att.MFIEMA1)
                        {
                            att.EarlyBird = true;
                            att.Mode = IndicatorMode.Buy;
                        }
                        else if (position.PositionSettings.Rule10 && att.STMode == IndicatorMode.Sell && att.RSIMode == IndicatorMode.Sell && att.MACDMode == IndicatorMode.Sell && Math.Abs(decimal.Subtract(att.MACD, att.SignalLine)) >= macdNoise && att.MFI < att.MFIEMA1)
                        {
                            att.Mode = IndicatorMode.Sell;
                        }
                        //new
                        else if (position.PositionSettings.Rule10 && att.STMode == IndicatorMode.Sell && att.RSIMode == IndicatorMode.Sell
                            && att.AdxMode == IndicatorMode.Sell && (fAttributes[i - 1].AdxMode == IndicatorMode.Buy || fAttributes[i - 2].AdxMode == IndicatorMode.Buy)
                            && att.MACDMode == IndicatorMode.Buy
                            && Math.Abs(decimal.Subtract(fAttributes[i - 1].MACD, fAttributes[i - 1].SignalLine)) >= macdNoise && att.MFI < att.MFIEMA1)
                        {
                            att.Mode = IndicatorMode.Sell;
                        }

                        else
                        {
                            att.Mode = fAttributes[i - 1].Mode;// IndicatorMode.Buy;
                        }


                        if (true)
                        {
                            if (position.PositionSettings.SellMFITriggered && att.Mode == IndicatorMode.Buy)
                            {
                                att.Mode = IndicatorMode.Sell;
                            }
                            else if (position.PositionSettings.SellMFITriggered && att.Mode == IndicatorMode.Sell)
                            {
                                position.PositionSettings.SellMFITriggered = false;
                            }


                            if (true)
                            {
                                if (att.MFI > fAttributes[i - 1].MFI && att.MACD > att.SignalLine && !position.PositionSettings.BuyMFITriggered
                                    && position.PositionSettings.BuyMFITrigger && (att.Adx < att.NegativeDIn || att.AdxMode == IndicatorMode.Buy))
                                {
                                    if (att.Adx < att.NegativeDIn)
                                    {

                                    }
                                    if (att.AdxMode == IndicatorMode.Buy)
                                    {

                                    }
                                    att.Mode = IndicatorMode.Buy;
                                    position.PositionSettings.BuyMFITriggered = true;
                                    position.PositionSettings.BuyMFITrigger = false;
                                }
                                if (att.MFI < 10 && !position.PositionSettings.BuyMFITrigger && !position.PositionSettings.BuyMFITriggered)
                                {
                                    position.PositionSettings.BuyMFITrigger = true;
                                }

                                if (position.PositionSettings.BuyMFITriggered)
                                {
                                    att.Mode = IndicatorMode.Buy;
                                }
                            }


                            //Reset MFI
                            if (true && position.PositionSettings.BuyMFITriggered && //att.MFI > att.MFIEMA1 &&
                                att.SignalLine > att.MACD
                                && (Math.Abs(decimal.Subtract(att.MACD, att.SignalLine)) > 2
                             )
                                //&& att.rs
                                //&& att.MFI < 80
                                && (fAttributes[i - 1].MACDMode == IndicatorMode.Buy || fAttributes[i - 2].MACDMode == IndicatorMode.Buy)
                                && att.AdxMode == IndicatorMode.Sell)
                            {
                                position.PositionSettings.BuyMFITriggered = false;
                                att.Mode = IndicatorMode.Sell;
                                position.PositionSettings.BuyMFITriggered = false;
                                position.PositionSettings.BuyMFITrigger = false;
                            }

                            if (!position.PositionSettings.BuyRSITriggered && position.PositionSettings.BuyRSITrigger && position.PositionSettings.Rule5
                                && att.RSIEMA1 > att.RSIEMA2 && att.MACD > att.SignalLine)
                            {
                                att.Mode = IndicatorMode.Buy;
                                position.PositionSettings.BuyRSITriggered = true;
                            }

                            if (!position.PositionSettings.BuyRSITrigger && position.PositionSettings.Rule5 && att.STMode == IndicatorMode.Sell && att.RSI < 20 && att.Mode == IndicatorMode.Sell)
                            {
                                position.PositionSettings.BuyRSITrigger = true;
                            }

                            if (position.PositionSettings.BuyRSITriggered && att.MACD > att.SignalLine)
                            {
                                att.Mode = IndicatorMode.Buy;
                            }
                        }
                    }



                    if (att.STMode == IndicatorMode.Sell && position.PositionSettings.WFFMode != IndicatorMode.None)
                    {
                        position.PositionSettings.WFFMode = IndicatorMode.None;
                    }

                    if (position.PositionSettings.WFFMode != IndicatorMode.None)
                    {
                        att.Mode = IndicatorMode.Sell;
                    }

                    endPrice = att.Close;
                    if (att.Mode != fAttributes[i - 1].Mode)
                    {

                        if (att.MFI > 80 && att.Mode == IndicatorMode.Buy)
                        {
                            position.PositionSettings.WFFMode = att.Mode;
                            att.Mode = IndicatorMode.Sell;
                        }
                        if (att.Adx < 25 && !position.PositionSettings.BuyMFITriggered)
                        {

                        }
                        if (att.Adx < att.PostiveDIn && att.Adx < att.NegativeDIn && !position.PositionSettings.BuyMFITriggered)
                        {
                            // att.Mode==fAttributes[i-1].Mode
                        }
                        if ((startPrice < endPrice) && att.Mode == IndicatorMode.Buy)
                        {
                            isLoss = true;
                        }
                        if ((startPrice > endPrice) && att.Mode == IndicatorMode.Sell)
                        {
                            isLoss = true;
                        }
                        startPrice = att.Close;
                        startDateTime = att.TimeStamp;
                    }


                }
            }
        }
        private static async Task<Margins[]> GetMargins()
        {

            Margins[] margins = null;
            HttpClient client = new HttpClient();
            HttpResponseMessage response = await client.GetAsync("https://api.kite.trade/margins/futures");
            if (response.IsSuccessStatusCode)
            {
                string response1 = await response.Content.ReadAsStringAsync();
                margins = JsonConvert.DeserializeObject<Margins[]>(response1);
            }
            return margins;
        }
        private static Settings CheckCrossOver(Settings positionSettings)
        {
            if (positionSettings.CurrentMode != positionSettings.PMode)
            {
                positionSettings.TradeOnCrossOver = false;
                return positionSettings;
            }
            positionSettings.CMode = IndicatorMode.None;
            return positionSettings;
        }
        private static async void ExecuteTradeByMargin(Position position)
        {
            try
            {
                Settings positionSettings = position.PositionSettings;

                if (positionSettings.TradeOnCrossOver == true)
                    positionSettings = CheckCrossOver(positionSettings);

                if (positionSettings.TradeOnCrossOverUpdate == true)
                {
                    positionSettings.CMode = IndicatorMode.None;
                    positionSettings.TradeOnCrossOver = true;
                    positionSettings.TradeOnCrossOverUpdate = false;
                }

                Console.WriteLine(positionSettings.TradingSymbol + " - " + positionSettings.CMode);

                if (positionSettings.CMode != IndicatorMode.None)
                {
                    //For First Time
                    if (positionSettings.IsCrossOver)
                        positionSettings.LastRecorded = GetIndianDateTime();

                    Dictionary<string, Quote> quote = GetQuote(positionSettings.Exchange, positionSettings.TradingSymbol);




                    //Cancel orders
                    List<Order> totalOrders = kite.GetOrders();
                    List<Order> orders = totalOrders.Where(x => x.Status.ToLower() == "open").ToList();


                    foreach (Order order in orders)
                    {
                        if (order.Tradingsymbol == positionSettings.TradingSymbol)
                            kite.CancelOrder(order.OrderId);
                    }

                    PositionResponse positions = GetPositions(kite);



                    //NIFTY18JULFUT
                    var future = positions.Net.Where(x => x.TradingSymbol == positionSettings.TradingSymbol).FirstOrDefault();
                    var futureCount = (future.Equals(null) || future.Quantity == 0) ? 0 : future.Quantity / positionSettings.LotSize;
                    bool IsNegative = futureCount < 0;


                    //CheckTarget
                    //if (System.Math.Abs(futureCount) > 0 && !positionSettings.IsTargetAchieved && positionSettings.CMode != IndicatorMode.None &&
                    //  positionSettings.Target > 0 && ((future.Realised + future.Unrealised) / System.Math.Abs(futureCount) >= positionSettings.Target))
                    bool targetSignal = false;
                    if (futureCount == positionSettings.TotalPosition)
                    {
                        positionSettings.MaxPostion = positionSettings.TotalPosition;
                        positionSettings.IsTargetAchieved = false;
                    }

                    //Points based profit target  1-50-1
                    if (!string.IsNullOrEmpty(positionSettings.PointsInfo))
                    {
                        string[] points = positionSettings.PointsInfo.Split(';');
                        if (points.Length > 0)
                        {
                            var quantityCount = future.Quantity / positionSettings.LotSize;
                            for (int i = 1; i <= quantityCount; i++)
                            {
                                foreach (string point in points)
                                {
                                    string[] pointStatus = point.Split('-');
                                    int futureSno = Convert.ToInt32(pointStatus[0]);
                                    int realPoint = Convert.ToInt32(pointStatus[1]);
                                    int status = Convert.ToInt32(pointStatus[2]);
                                    if (status == 1 && futureSno == i && realPoint > 0)
                                    {                                     //Sell Target
                                        if (future.Quantity != 0 && future.Quantity < 0)
                                        {
                                            if (future.DaySellPrice != 0 || future.SellPrice != 0)
                                            {
                                                if (future.DaySellPrice != 0 && future.DaySellPrice - realPoint < future.LastPrice)
                                                {
                                                    positionSettings.MaxPostion = positionSettings.MaxPostion - 1;
                                                    targetSignal = true;
                                                }
                                                else if (future.SellPrice != 0 && future.SellPrice - realPoint < future.LastPrice)
                                                {
                                                    positionSettings.MaxPostion = positionSettings.MaxPostion - 1;
                                                    targetSignal = true;
                                                }
                                            }
                                        }
                                        //Buy Target
                                        if (future.Quantity != 0 && future.Quantity > 0)
                                        {
                                            if (future.DayBuyPrice != 0 || future.BuyPrice != 0)
                                            {
                                                if (future.DayBuyPrice != 0 && future.DayBuyPrice + realPoint < future.LastPrice)
                                                {
                                                    positionSettings.MaxPostion = positionSettings.MaxPostion - 1;
                                                    targetSignal = true;
                                                }
                                                if (future.BuyPrice != 0 && future.BuyPrice + realPoint < future.LastPrice)
                                                {
                                                    positionSettings.MaxPostion = positionSettings.MaxPostion - 1;
                                                    targetSignal = true;
                                                }

                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }


                    // if (System.Math.Abs(futureCount) > 0 && !positionSettings.IsTargetAchieved && positionSettings.CMode != IndicatorMode.None &&
                    //positionSettings.Target > 0 && ((future.Unrealised) / System.Math.Abs(futureCount) >= positionSettings.Target))
                    // {
                    //     if (positionSettings.SquareOffTarget <= 0) positionSettings.SquareOffTarget = 1;
                    //     positionSettings.MaxPostion = positionSettings.MaxPostion - positionSettings.SquareOffTarget;

                    //     if (positionSettings.MaxPostion < 0) positionSettings.MaxPostion = 0;
                    //     positionSettings.IsTargetAchieved = true;
                    //     targetSignal = true;
                    // }

                    if (positionSettings.TradeOnCrossOverUpdate == false && positionSettings.TradeOnCrossOver == false &&
                        futureCount == 0 && positionSettings.MaxPostion > 0 && positionSettings.CMode == IndicatorMode.None)
                    {
                        positionSettings.CMode = positionSettings.CurrentMode;
                    }

                    //Reset Target on CrossOver
                    if (positionSettings.IsCrossOver && positionSettings.PMode != IndicatorMode.None && positionSettings.TotalPosition > 0)
                    {
                        positionSettings.IsTargetAchieved = false;
                        positionSettings.MaxPostion = positionSettings.TotalPosition;
                    }
                    bool IsMargin = false;
                    try
                    {
                        UserMarginsResponse userMarginsResponse = kite.GetMargins();

                        if (userMarginsResponse.Equity.Net > positionSettings.Margin * positionSettings.TotalPosition)
                        {
                            //Margin available
                            IsMargin = true;//Changed 06/12/2018
                        }
                    }
                    catch
                    {

                    }

                    ////Filter more than one
                    //var futureDay = positions.Day.Where(x => x.TradingSymbol == positionSettings.TradingSymbol).FirstOrDefault();
                    //if (futureDay.DaySellPrice > 0 && futureDay.DayBuyPrice > 0 && futureDay.DaySellPrice < futureDay.DayBuyPrice)
                    //{
                    //    positionSettings.CMode = positionSettings.pcCMode;
                    //}

                    //Only Buy
                    if (positionSettings.BuyOnlyOption == true && positionSettings.CMode == IndicatorMode.Sell) positionSettings.MaxPostion = 0;

                    if (positionSettings.BuyOnlyOption == true && positionSettings.CMode == IndicatorMode.Buy)
                        positionSettings.MaxPostion = positionSettings.TotalPosition;

                    //Option Lot Calculation
                    decimal lotCanBuy = 0;
                    if (positionSettings.InstrumentType == "CE" || positionSettings.InstrumentType == "PE")
                    {
                        if (positionSettings.TradeAmount != 0)
                        {
                            decimal oneLot = positionSettings.LotSize * quote.First().Value.Bids.First().Price;
                            if (oneLot != 0)
                            {
                                lotCanBuy = positionSettings.TradeAmount / oneLot;
                                positionSettings.MaxPostion = Convert.ToInt32(Math.Truncate(lotCanBuy));
                            }
                            else
                                return;
                        }
                    }



                    // Expiry Date -Square off
                    DateTime indianDateTime = GetIndianDateTime();
                    if (positionSettings.Expiry.ToString("yyyy-MM-dd") == Convert.ToDateTime(indianDateTime).ToString("yyyy-MM-dd"))
                    {
                        if (indianDateTime.Hour >= 15 && indianDateTime.Minute > 15)
                        {
                            if (IsNegative) positionSettings.CMode = IndicatorMode.Buy;
                            if (!IsNegative) positionSettings.CMode = IndicatorMode.Sell;
                            //Update Active flag
                            positionSettings.Active = false;
                            positionSettings.CurrentMonthExpired = true;
                            ActivateNextMonthSeries(positionSettings);
                        }
                    }

                    if (positionSettings.BuyOnlyOption)
                    {
                        int quant = (IsNegative) ? positionSettings.MaxPostion - System.Math.Abs(futureCount) : positionSettings.MaxPostion - futureCount;
                        var bidPrice = quote.First().Value.Bids.First().Price;
                        var offerPrice = quote.First().Value.Offers.First().Price; //Chaging bid to offer
                        if (IndicatorMode.Buy == positionSettings.CMode)
                        {
                            if (IsNegative && quant == 0)
                            {
                                quant = System.Math.Abs(futureCount);
                                PlaceOrder(positionSettings.Exchange, positionSettings.TradingSymbol, Constants.TRANSACTION_TYPE_BUY, System.Math.Abs(quant) * positionSettings.LotSize, bidPrice, Constants.ORDER_TYPE_LIMIT, Constants.PRODUCT_NRML);
                                Log(positionSettings.Exchange, positionSettings.TradingSymbol, positionSettings.NewShortEMA, positionSettings.NewLongEMA, Convert.ToInt32(futureCount.ToString() + "11" + positionSettings.MaxPostion.ToString()), bidPrice, positionSettings.CMode, positionSettings);
                                Console.WriteLine("Cooling period");
                                Thread.Sleep(15000);
                            }
                            else
                            {
                                if (!IsNegative && quant > 0)
                                {
                                    PlaceOrder(positionSettings.Exchange, positionSettings.TradingSymbol, Constants.TRANSACTION_TYPE_BUY, System.Math.Abs(quant) * positionSettings.LotSize, offerPrice, Constants.ORDER_TYPE_LIMIT, Constants.PRODUCT_NRML);
                                    Log(positionSettings.Exchange, positionSettings.TradingSymbol, positionSettings.NewShortEMA, positionSettings.NewLongEMA,
                                        Convert.ToInt32(futureCount.ToString() + "35" + positionSettings.MaxPostion.ToString()), offerPrice, positionSettings.CMode, positionSettings);
                                    Console.WriteLine("Cooling period");
                                    Thread.Sleep(15000);
                                }
                            }
                        }
                        if (IndicatorMode.Sell == positionSettings.CMode && quant > 0)
                        {
                            quant = futureCount;
                            PlaceOrder(positionSettings.Exchange, positionSettings.TradingSymbol, Constants.TRANSACTION_TYPE_SELL, System.Math.Abs(quant) * positionSettings.LotSize, offerPrice, Constants.ORDER_TYPE_LIMIT, Constants.PRODUCT_NRML);
                            Log(positionSettings.Exchange, positionSettings.TradingSymbol, positionSettings.NewShortEMA, positionSettings.NewLongEMA,
                                Convert.ToInt32(futureCount.ToString() + "35" + positionSettings.MaxPostion.ToString()), offerPrice, positionSettings.CMode, positionSettings);
                            Console.WriteLine("Cooling period");
                            Thread.Sleep(30000);
                        }
                        return;
                    }

                    if (targetSignal)
                    {
                        int quant = (IsNegative) ? positionSettings.MaxPostion - System.Math.Abs(futureCount) : positionSettings.MaxPostion - futureCount;
                        var bidPrice = quote.First().Value.Bids.First().Price;
                        var offerPrice = quote.First().Value.Offers.First().Price; //Chaging bid to offer
                        if (!IsNegative && quant > 0)
                        {
                            PlaceOrder(positionSettings.Exchange, positionSettings.TradingSymbol, Constants.TRANSACTION_TYPE_SELL, System.Math.Abs(quant) * positionSettings.LotSize, offerPrice, Constants.ORDER_TYPE_LIMIT, Constants.PRODUCT_NRML);
                            Log(positionSettings.Exchange, positionSettings.TradingSymbol, positionSettings.NewShortEMA, positionSettings.NewLongEMA,
                                Convert.ToInt32(futureCount.ToString() + "35" + positionSettings.MaxPostion.ToString()), offerPrice, positionSettings.CMode, positionSettings);
                            Console.WriteLine("Cooling period");
                            Thread.Sleep(30000);
                        }
                        else if (IsNegative && quant > 0)
                        {
                            PlaceOrder(positionSettings.Exchange, positionSettings.TradingSymbol, Constants.TRANSACTION_TYPE_BUY, System.Math.Abs(quant) * positionSettings.LotSize, bidPrice, Constants.ORDER_TYPE_LIMIT, Constants.PRODUCT_NRML);
                            Log(positionSettings.Exchange, positionSettings.TradingSymbol, positionSettings.NewShortEMA, positionSettings.NewLongEMA, Convert.ToInt32(futureCount.ToString() + "11" + positionSettings.MaxPostion.ToString()), bidPrice, positionSettings.CMode, positionSettings);
                            Console.WriteLine("Cooling period");
                            Thread.Sleep(15000);
                        }
                    }

                    //Execute Trade without any target

                    if (IndicatorMode.Buy == positionSettings.CMode)
                    {
                        int quant = (IsNegative) ? positionSettings.MaxPostion + System.Math.Abs(futureCount) : positionSettings.MaxPostion - futureCount;
                        if (futureCount == 0)
                            quant = positionSettings.MaxPostion;
                        if (!IsMargin && !IsNegative && quant > 0 && futureCount > 0)
                        {
                            quant = futureCount; //so that margin will be back.
                        }
                        //if(!IsNegative && ())
                        var bidPrice = quote.Count != 0 ? quote.First().Value.Bids.First().Price : 0;
                        var offerPrice = quote.Count != 0 ? quote.First().Value.Offers.First().Price : 0;
                        //Chaging bid to offer
                        //if (IsNegative && quant != 0)
                        //{
                        //    PlaceOrder(positionSettings.Exchange, positionSettings.TradingSymbol, Constants.TRANSACTION_TYPE_SELL, System.Math.Abs(quant) * positionSettings.LotSize, offerPrice, Constants.ORDER_TYPE_LIMIT, Constants.PRODUCT_NRML);
                        //    Log(positionSettings.Exchange, positionSettings.TradingSymbol, positionSettings.NewShortEMA, positionSettings.NewLongEMA,
                        //        Convert.ToInt32(futureCount.ToString() + "35" + positionSettings.MaxPostion.ToString()), offerPrice, positionSettings.CMode, positionSettings);
                        //    Console.WriteLine("Cooling period");
                        //    Thread.Sleep(30000);
                        //}
                        //else 
                        if (bidPrice != 0)
                            bidPrice = bidPrice + 0.10M;

                        if (quant != 0)
                        {
                            PlaceOrder(positionSettings.Exchange, positionSettings.TradingSymbol, Constants.TRANSACTION_TYPE_BUY, System.Math.Abs(quant) * positionSettings.LotSize, bidPrice, Constants.ORDER_TYPE_LIMIT, Constants.PRODUCT_NRML);
                            Log(positionSettings.Exchange, positionSettings.TradingSymbol, positionSettings.NewShortEMA, positionSettings.NewLongEMA, Convert.ToInt32(futureCount.ToString() + "11" + positionSettings.MaxPostion.ToString()), bidPrice, positionSettings.CMode, positionSettings);
                            Console.WriteLine("Cooling period");
                            Thread.Sleep(30000);
                        }
                    }
                    else if (IndicatorMode.Sell == positionSettings.CMode)
                    {
                        int quant = positionSettings.MaxPostion + futureCount;

                        //if(!IsNegative && ())
                        var bidPrice = quote.Count != 0 ? quote.First().Value.Bids.First().Price : 0;
                        var offerPrice = quote.Count != 0 ? quote.First().Value.Offers.First().Price : 0;

                        //var bidPrice = quote.First().Value.Bids.First().Price;
                        //var offerPrice = quote.First().Value.Offers.First().Price;
                        if (offerPrice != 0)
                            offerPrice = offerPrice - 0.10M;
                        if (!IsMargin && quant != 0 && futureCount > 0)
                        {
                            quant = futureCount;
                        }

                        if (futureCount == 0)
                            quant = positionSettings.MaxPostion;
                        if (quant != 0)
                        {
                            PlaceOrder(positionSettings.Exchange, positionSettings.TradingSymbol, Constants.TRANSACTION_TYPE_SELL, System.Math.Abs(quant) * positionSettings.LotSize, offerPrice, Constants.ORDER_TYPE_LIMIT, Constants.PRODUCT_NRML);
                            Log(positionSettings.Exchange, positionSettings.TradingSymbol, positionSettings.NewShortEMA, positionSettings.NewLongEMA,
                                Convert.ToInt32(futureCount.ToString() + "33" + positionSettings.MaxPostion.ToString()), offerPrice, positionSettings.CMode, positionSettings);
                            Console.WriteLine("Cooling period");
                            Thread.Sleep(30000);
                        }
                    }
                }
                Console.WriteLine("Updating Database...!!!");
                UpdateDB(positionSettings);
            }
            catch (Exception ex)
            {
                ExceptionLog(ex);
            }
            Thread.Sleep(2000);
        }

        private static void ActivateNextMonthSeries(Settings positionSettings)
        {
            string connectionString = ConfigurationManager.AppSettings["KiteDB"];
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string tradingSymbolNextMonth = string.Empty;
                if (GetIndianDateTime().Month != 12)
                {
                    tradingSymbolNextMonth = positionSettings.TradingSymbol.Substring(0,
                       positionSettings.TradingSymbol.IndexOf(positionSettings.Expiry.Year.ToString().Substring(2, 2))) + positionSettings.Expiry.Year.ToString().Substring(2, 2)
                       + CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(GetIndianDateTime().Month + 1).ToUpperInvariant() + "FUT";
                }
                else
                {
                    tradingSymbolNextMonth = positionSettings.TradingSymbol.Substring(0,
                       positionSettings.TradingSymbol.IndexOf(positionSettings.Expiry.Year.ToString().Substring(2, 2))) + (positionSettings.Expiry.Year + 1).ToString().Substring(2, 2)
                       + CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(1).ToUpperInvariant() + "FUT";
                }
                string commandText = "UPDATE [PostionSettings] SET [Active] = 'True' WHERE [TradingSymbol]='" + tradingSymbolNextMonth + "'";

                SqlCommand command = new SqlCommand(commandText, conn)
                {
                    CommandType = System.Data.CommandType.Text,
                };
                command.ExecuteNonQuery();
                conn.Close();
            }
        }

        private static void ExceptionLog(Exception exception)
        {
            string connectionString = ConfigurationManager.AppSettings["KiteDB"];
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string commandText = "INSERT INTO ExceptionLog (Message, InnerException, StackTrace,[CreateTime]) SELECT '" + exception.Message.ToString() +
                                                                "','" + exception.InnerException +
                                                                "','" + exception.StackTrace.ToString() +
                                                                "','" + GetIndianDateTime().ToString("yyyy-MM-dd HH:mm:ss") + "'";

                SqlCommand command = new SqlCommand(commandText, conn)
                {
                    CommandType = System.Data.CommandType.Text
                };
                command.ExecuteNonQuery();
                conn.Close();
            }
        }
        private static void UpdateSuperTrendinDB(Settings positionSettings)
        {
            string connectionString = ConfigurationManager.AppSettings["KiteDB"];
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string commandText = string.Empty;

                commandText = "UPDATE [PostionSettings] SET [IndicatorParmOne] =" + positionSettings.IndicatorParmOne +
                                                               " WHERE [TradingSymbol]='" + positionSettings.TradingSymbol +
                                                               "'";

                SqlCommand command = new SqlCommand(commandText, conn)
                {
                    CommandType = System.Data.CommandType.Text,
                };
                command.ExecuteNonQuery();
                conn.Close();
            }
        }
        private static void UpdateDB(Settings positionSettings)
        {
            string connectionString = ConfigurationManager.AppSettings["KiteDB"];
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string commandText = string.Empty;
                if (positionSettings.CurrentMonthExpired)
                {
                    commandText = "UPDATE [PostionSettings] SET [LongEMA] =" + positionSettings.NewLongEMA +
                                                                   ", [ShortEMA]=" + positionSettings.NewShortEMA +
                                                                   ", [TradeOnCrossOverUpdate]='" + positionSettings.TradeOnCrossOverUpdate +
                                                                   "', [TradeOnCrossOver]='" + positionSettings.TradeOnCrossOver +
                                                                   "', [FMode]='" + positionSettings.CurrentMode +
                                                                   "', [StableMode]='" + positionSettings.CMode +
                                                                   "', [LastRecorded]='" + positionSettings.LastRecorded.ToString("yyyy-MM-dd HH:mm:ss") +
                                                                   "', [IsTargetAchieved] = '" + positionSettings.IsTargetAchieved.ToString() +
                                                                   "', [MaxPostion] = '" + positionSettings.MaxPostion +
                                                                   "', [LastUpdateTime] = '" + GetIndianDateTime().ToString("yyyy-MM-dd HH:mm:ss") +
                                                                   "', [Active] = '" + positionSettings.Active +
                                                                   "' WHERE [TradingSymbol]='" + positionSettings.TradingSymbol +
                                                                   "'";
                }
                else
                {
                    commandText = "UPDATE [PostionSettings] SET [LongEMA] =" + positionSettings.NewLongEMA +
                                                                  ", [ShortEMA]=" + positionSettings.NewShortEMA +
                                                                  ", [TradeOnCrossOverUpdate]='" + positionSettings.TradeOnCrossOverUpdate +
                                                                  "', [TradeOnCrossOver]='" + positionSettings.TradeOnCrossOver +
                                                                  "', [FMode]='" + positionSettings.CurrentMode +
                                                                  "', [StableMode]='" + positionSettings.CMode +
                                                                  "', [LastRecorded]='" + positionSettings.LastRecorded.ToString("yyyy-MM-dd HH:mm:ss") +
                                                                  "', [IsTargetAchieved] = '" + positionSettings.IsTargetAchieved.ToString() +
                                                                  "', [MaxPostion] = '" + positionSettings.MaxPostion +
                                                                  "', [LastUpdateTime] = '" + GetIndianDateTime().ToString("yyyy-MM-dd HH:mm:ss") +
                                                                  "', [CreateTime] = '" + positionSettings.CreateTime.ToString("yyyy-MM-dd HH:mm:ss") +

                                                                  // "', [Active] = '" + positionSettings.Active +
                                                                  "' WHERE [TradingSymbol]='" + positionSettings.TradingSymbol +
                                                                  "'";
                }

                SqlCommand command = new SqlCommand(commandText, conn)
                {
                    CommandType = System.Data.CommandType.Text,
                };
                command.ExecuteNonQuery();
                conn.Close();
            }
        }

        private static void Log(string exchange, string tradingSymbol, decimal item1, decimal item2, int futureCount, decimal bidPrice, IndicatorMode currentMode, Settings positionSettings)
        {
            string connectionString = ConfigurationManager.AppSettings["KiteDB"];
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string currentSettings = JsonConvert.SerializeObject(positionSettings);
                SqlCommand command = new SqlCommand("INSERT INTO [LogSettings]([Exchange],[TradingSymbol],[Item1],[Item2],[BidPrice],[Mode],[Count],CreateTime, Json) " +
                    "SELECT '" + exchange + "', '" + tradingSymbol + "', " + item1 + ", " + item2 + ", " + bidPrice + ",'" + currentMode.ToString() + "', "
                    + futureCount + ",'" + GetIndianDateTime().ToString("yyyy-MM-dd HH:mm:ss") + "','" + currentSettings + "'", conn);

                command.CommandType = System.Data.CommandType.Text;
                command.ExecuteNonQuery();
                conn.Close();
            }
        }


        private static readonly Object thisLockPlaceOrder = new object();
        private static void PlaceOrder(string exchange, string tradingSymbol, string tRANSACTION_TYPE, int quantity, decimal bidPrice, string oRDER_TYPE, string ProductType)
        {
            lock (thisLockPlaceOrder)
            {

                kite.PlaceOrder(Exchange: exchange,
                                            TradingSymbol: tradingSymbol,
                                            TransactionType: tRANSACTION_TYPE,
                                            Quantity: quantity,
                                            Price: bidPrice,
                                            OrderType: oRDER_TYPE, //Check this
                                            Product: ProductType
                                        );
                Thread.Sleep(200);
            }
        }

        private static readonly Object thisLockGetPositions = new object();
        public static PositionResponse GetPositions(Kite kite)
        {
            try
            {
                lock (thisLockGetPositions)
                {
                    Thread.Sleep(500);
                    return kite.GetPositions();
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private static readonly Object thisLockGetQuote = new object();
        public static Dictionary<string, Quote> GetQuote(string exchange, string tradingSymbol)
        {
            try
            {
                lock (thisLockGetQuote)
                {
                    Thread.Sleep(1000);
                    Dictionary<string, Quote> quote = new Dictionary<string, Quote>();
                    quote = kite.GetQuote(InstrumentId: new string[] { exchange + ":" + tradingSymbol });
                    return quote;
                }
            }
            catch
            {
                return null;
            }
        }

        private static readonly Object thisLockGetHistoricalData = new object();
        internal static List<Historical> GetHistoricalData(Kite kite, string instrumentToken, DateTime dateTime, DateTime timeInIndia, string iNTERVAL_60MINUTE, bool v)
        {
            try
            {
                lock (thisLockGetHistoricalData)
                {

                    Thread.Sleep(400);
                    List<Historical> historical = kite.GetHistoricalData(
                   InstrumentToken: instrumentToken,
                   FromDate: dateTime,   // 2016-01-01 12:50:00 AM
                   ToDate: timeInIndia,    // 2016-01-01 01:10:00 PM
                   Interval: iNTERVAL_60MINUTE,
                   Continuous: v
                );
                    return historical;
                }
            }
            catch (Exception ex)
            {
                ExceptionLog(ex);
                Thread.Sleep(5000);
            }
            return new List<Historical>();
        }

        private static void OnTokenExpire()
        {
            Console.WriteLine("Need to login again");
        }

        private static void OnConnect()
        {
            Console.WriteLine("Connected ticker");
        }

        private static void OnClose()
        {
            Console.WriteLine("Closed ticker");
        }

        private static void OnError(string Message)
        {
            Console.WriteLine("Error: " + Message);
        }

        private static void OnNoReconnect()
        {
            Console.WriteLine("Not reconnecting");
        }

        private static void OnReconnect()
        {
            Console.WriteLine("Reconnecting");
        }
        public static DateTime GetIndianDateTime()
        {
            var indianZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            var timeInIndia = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, indianZone);//.ToString("yyyy-MM-dd HH:mm:ss");
                                                                                           //timeInIndia = Convert.ToDateTime("28-12-2018 15:16:32.000");
            return timeInIndia;
        }


        private static void OnTick(Tick TickData)
        {
            Console.WriteLine("Tick " + Utils.JsonSerialize(TickData));
            string connectionString = ConfigurationManager.AppSettings["KiteDB"];
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                DateTime now = GetIndianDateTime();
                DateTime date = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0);

                string commandText = "SELECT 1 FROM HistoricalData WHERE CONVERT(VARCHAR(13), LastUpdateTime, 120) +':00:00' ='" + date.ToString("yyyy-MM-dd HH:00:00") + "'";
                SqlCommand command = new SqlCommand(commandText, conn)
                {
                    CommandType = System.Data.CommandType.Text
                };
                var output = command.ExecuteScalar();
                if (output == null)

                    commandText = "INSERT INTO [HistoricalData]([InstrumentToken],LastTradeTime,Change,[Open],[High],[Low],[Close],[LastPrice],[Timestamp],[LastUpdateTime])" +
                       " SELECT " + TickData.InstrumentToken
                       + ",'" + (TickData.LastTradeTime.HasValue ? TickData.LastTradeTime.Value.ToString("yyyy-MM-dd HH:mm:ss") : string.Empty)
                       + "','" + TickData.Change
                       + "','" + TickData.Open
                       + "','" + TickData.High
                       + "','" + TickData.Low
                       + "','" + TickData.Close
                       + "','" + TickData.LastPrice
                       + "','" + (TickData.Timestamp.HasValue ? TickData.Timestamp.Value.ToString("yyyy-MM-dd HH:mm:ss") : string.Empty)
                       + "','" + GetIndianDateTime().ToString("yyyy-MM-dd HH:mm:ss") + "'";

                else
                    commandText = "UPDATE [HistoricalData] SET [InstrumentToken] =" + TickData.InstrumentToken +
                                                                   ", LastTradeTime='" + (TickData.LastTradeTime.HasValue ? TickData.LastTradeTime.Value.ToString("yyyy-MM-dd HH:mm:ss") : string.Empty)
                                                                   + "',Change='" + TickData.Change +
                                                                   "',[Open]='" + TickData.Open +
                                                                   "',[High]='" + TickData.High +
                                                                   "',[Low]='" + TickData.Low +
                                                                   "',[Close]='" + TickData.Close +
                                                                   "',[LastPrice]='" + TickData.LastPrice +
                                                                   "',[Timestamp]='" + (TickData.Timestamp.HasValue ? TickData.Timestamp.Value.ToString("yyyy-MM-dd HH:mm:ss") : string.Empty) +
                                                                   "',[LastUpdateTime]='" + GetIndianDateTime().ToString("yyyy-MM-dd HH:mm:ss") +

                                                                   "' WHERE [InstrumentToken]='" + TickData.InstrumentToken + "'";

                command = new SqlCommand(commandText, conn)
                {
                    CommandType = System.Data.CommandType.Text
                };
                command.ExecuteNonQuery();
                conn.Close();
            }
        }

        private static void OnOrderUpdate(Order OrderData)
        {
            Console.WriteLine("OrderUpdate " + Utils.JsonSerialize(OrderData));
        }
        private static List<string> InstrumentsToBackTest()
        {
            List<string> Instruments = new List<string>();
            Instruments.Add("BANKNIFTY");
            Instruments.Add("NIFTY");
            Instruments.Add("ICICIBANK");
            Instruments.Add("RELIANCE");
            Instruments.Add("SBIN");
            Instruments.Add("TCS");
            Instruments.Add("MARUTI");
            Instruments.Add("TATASTEEL");
            Instruments.Add("YESBANK");
            Instruments.Add("AXISBANK");
            Instruments.Add("INFY");
            Instruments.Add("HDFCBANK");
            Instruments.Add("HDFC");
            Instruments.Add("HINDALCO");
            Instruments.Add("EICHERMOT");
            Instruments.Add("TATAMOTORS");
            Instruments.Add("M&M");
            Instruments.Add("SUNPHARMA");
            Instruments.Add("BAJFINANCE");
            Instruments.Add("KOTAKBANK");
            Instruments.Add("LT");
            Instruments.Add("TECHM");
            Instruments.Add("PNB");
            Instruments.Add("ASHOKLEY");
            Instruments.Add("ITC");
            Instruments.Add("MINDTREE");
            Instruments.Add("BANKBARODA");
            Instruments.Add("VEDL");
            Instruments.Add("JUBLFOOD");
            Instruments.Add("BHARTIARTL");
            Instruments.Add("JINDALSTEL");
            Instruments.Add("HINDUNILVR");
            Instruments.Add("ESCORTS");
            Instruments.Add("INDUSINDBK");
            Instruments.Add("HEROMOTOCO");
            Instruments.Add("AUROPHARMA");
            Instruments.Add("IBULHSGFIN");
            Instruments.Add("TITAN");
            Instruments.Add("ASIANPAINT");
            Instruments.Add("RELCAPITAL");
            Instruments.Add("BANKINDIA");
            Instruments.Add("LUPIN");
            Instruments.Add("CANBK");
            Instruments.Add("BEML");
            Instruments.Add("JSWSTEEL");
            Instruments.Add("BPCL");
            Instruments.Add("HCLTECH");
            Instruments.Add("UPL");
            Instruments.Add("NCC");
            Instruments.Add("L&TFH");
            Instruments.Add("LICHSGFIN");
            Instruments.Add("FEDERALBNK");
            Instruments.Add("DLF");
            Instruments.Add("BAJAJ-AUTO");
            Instruments.Add("RELINFRA");
            Instruments.Add("SRTRANSFIN");
            Instruments.Add("DRREDDY");
            Instruments.Add("SAIL");
            Instruments.Add("UNIONBANK");
            Instruments.Add("ADANIPORTS");
            Instruments.Add("INFRATEL");
            Instruments.Add("PEL");
            Instruments.Add("TATAELXSI");
            Instruments.Add("ONGC");
            Instruments.Add("BHARATFORG");
            Instruments.Add("ZEEL");
            Instruments.Add("INDIGO");
            Instruments.Add("NESTLEIND");
            Instruments.Add("BHEL");
            Instruments.Add("BATAINDIA");
            Instruments.Add("MCDOWELL-N");
            Instruments.Add("HINDPETRO");
            Instruments.Add("HAVELLS");
            Instruments.Add("IDFCBANK");
            Instruments.Add("UJJIVAN");
            Instruments.Add("SRF");
            Instruments.Add("RCOM");
            Instruments.Add("PVR");
            Instruments.Add("PFC");
            Instruments.Add("RECLTD");
            Instruments.Add("DHFL");
            Instruments.Add("WIPRO");
            Instruments.Add("ULTRACEMCO");
            Instruments.Add("SUNTV");
            Instruments.Add("BEL");
            Instruments.Add("NIITTECH");
            Instruments.Add("APOLLOTYRE");
            Instruments.Add("PAGEIND");
            Instruments.Add("GRASIM");
            Instruments.Add("NBCC");
            Instruments.Add("COALINDIA");
            Instruments.Add("VOLTAS");
            Instruments.Add("GAIL");
            Instruments.Add("IOC");
            Instruments.Add("CIPLA");
            Instruments.Add("ACC");
            Instruments.Add("APOLLOHOSP");
            Instruments.Add("PETRONET");
            Instruments.Add("BAJAJFINSV");
            Instruments.Add("BIOCON");
            Instruments.Add("TVSMOTOR");
            Instruments.Add("MOTHERSUMI");
            Instruments.Add("ORIENTBANK");
            Instruments.Add("NTPC");
            Instruments.Add("AMBUJACEM");
            Instruments.Add("PCJEWELLER");
            Instruments.Add("RBLBANK");
            Instruments.Add("JUSTDIAL");
            Instruments.Add("CENTURYTEX");
            Instruments.Add("UBL");
            Instruments.Add("DIVISLAB");
            Instruments.Add("COLPAL");
            //Instruments.Add("JETAIRWAYS");
            //Instruments.Add("KTKBANK");
            //Instruments.Add("MUTHOOTFIN");
            //Instruments.Add("CEATLTD");
            //Instruments.Add("SIEMENS");
            //Instruments.Add("MARICO");
            //Instruments.Add("JISLJALEQS");
            //Instruments.Add("M&MFIN");
            //Instruments.Add("GLENMARK");
            //Instruments.Add("TORNTPHARM");
            //Instruments.Add("ADANIENT");
            //Instruments.Add("INDIANB");
            //Instruments.Add("BALKRISIND");
            //Instruments.Add("INDIACEM");
            //Instruments.Add("DABUR");
            //Instruments.Add("DCBBANK");
            //Instruments.Add("POWERGRID");
            //Instruments.Add("IDEA");
            //Instruments.Add("IGL");
            //Instruments.Add("RAYMOND");
            //Instruments.Add("TATAGLOBAL");
            //Instruments.Add("HEXAWARE");
            //Instruments.Add("IDFC");
            //Instruments.Add("KSCL");
            //Instruments.Add("IDBI");
            //Instruments.Add("STAR");
            //Instruments.Add("NMDC");
            //Instruments.Add("TORNTPOWER");
            //Instruments.Add("CESC");
            //Instruments.Add("MANAPPURAM");
            //Instruments.Add("GODREJCP");
            //Instruments.Add("CONCOR");
            //Instruments.Add("EQUITAS");
            //Instruments.Add("BHARATFIN");
            //Instruments.Add("RPOWER");
            //Instruments.Add("BRITANNIA");
            //Instruments.Add("NATIONALUM");
            //Instruments.Add("PIDILITIND");
            //Instruments.Add("EXIDEIND");
            //Instruments.Add("TATAMTRDVR");
            //Instruments.Add("TATACOMM");
            //Instruments.Add("TATAPOWER");
            //Instruments.Add("TATACHEM");
            //Instruments.Add("WOCKPHARMA");
            //Instruments.Add("SYNDIBANK");
            //Instruments.Add("BERGEPAINT");
            //Instruments.Add("KPIT");
            //Instruments.Add("KAJARIACER");
            //Instruments.Add("CHOLAFIN");
            //Instruments.Add("GMRINFRA");
            //Instruments.Add("AJANTPHARM");
            //Instruments.Add("ALBK");
            //Instruments.Add("DISHTV");
            //Instruments.Add("CADILAHC");
            //Instruments.Add("HINDZINC");
            //Instruments.Add("IRB");
            //Instruments.Add("AMARAJABAT");
            //Instruments.Add("MFSL");
            //Instruments.Add("ICICIPRULI");
            //Instruments.Add("NIFTYIT");
            //Instruments.Add("MCX");
            //Instruments.Add("GODFRYPHLP");
            //Instruments.Add("CANFINHOME");
            //Instruments.Add("ARVIND");
            //Instruments.Add("ENGINERSIN");
            //Instruments.Add("RAMCOCEM");
            //Instruments.Add("SHREECEM");
            //Instruments.Add("PTC");
            //Instruments.Add("GODREJIND");
            //Instruments.Add("REPCOHOME");
            //Instruments.Add("GSFC");
            //Instruments.Add("CUMMINSIND");
            //Instruments.Add("TV18BRDCST");
            //Instruments.Add("VGUARD");
            //Instruments.Add("CGPOWER");
            //Instruments.Add("SOUTHBANK");
            //Instruments.Add("SREINFRA");
            //Instruments.Add("JPASSOCIAT");
            //Instruments.Add("SUZLON");
            //Instruments.Add("MRF");
            //Instruments.Add("OIL");
            //Instruments.Add("MGL");
            //Instruments.Add("CHENNPETRO");
            //Instruments.Add("ADANIPOWER");
            //Instruments.Add("NHPC");
            //Instruments.Add("BOSCHLTD");
            //Instruments.Add("INFIBEAM");
            //Instruments.Add("IFCI");
            //Instruments.Add("CASTROLIND");
            //Instruments.Add("OFSS");
            //Instruments.Add("MRPL");
            return Instruments;
        }
    }

    public class Margins
    {
        public decimal Margin { get; set; }
        public decimal co_lower { get; set; }
        public decimal mis_multiplier { get; set; }
        public string TradingSymbol { get; set; }
        public decimal co_upper { get; set; }
        public decimal nrml_margin { get; set; }
        public decimal mis_margin { get; set; }
    }
}
