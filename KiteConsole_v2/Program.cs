using KiteConnect;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
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
            Initiate(string.Empty, kite);
            Console.ReadLine();

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
                                PositionSettings positionSettings = new PositionSettings
                                {
                                    TradingSymbol = dr["TradingSymbol"].ToString(),
                                    Exchange = dr["Exchange"].ToString(),
                                    InstrumentToken = dr["InstrumentToken"].ToString(),
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
                                    Interval = Convert.ToString(dr["Interval"])
                                };

                                if (string.IsNullOrEmpty(positionSettings.Indicator)) positionSettings.Indicator = "EMA";

                                if (positionSettings.Indicator == "EMA")
                                    reqIndicator = new EMAv1();
                                if (positionSettings.Indicator == "ST")
                                    reqIndicator = new SuperTrend();
                                else if (positionSettings.Indicator == "SMI")
                                    reqIndicator = new SMI();

                                positionSettings = reqIndicator.StartLooking(positionSettings, kite);

                                Console.WriteLine(positionSettings.TradingSymbol + " - " + positionSettings.CMode);
                                if (positionSettings.TradingSymbol.Contains("BAJFINANCE"))
                                    ExecuteTradeByMargin(positionSettings);
                                else
                                    ExecuteTrade(positionSettings);
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                ExceptionLog(ex);
                Thread.Sleep(1000 * 60);
                InitiateIndicator(null, kite);
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

        private static async void ExecuteTradeByMargin(PositionSettings positionSettings)
        {
            try
            {
                if (positionSettings.CMode != IndicatorMode.None)
                {
                    //For First Time
                    if (positionSettings.IsCrossOver)
                        positionSettings.LastRecorded = GetIndianDateTime();

                    Dictionary<string, Quote> quote = GetQuote(positionSettings.Exchange, positionSettings.TradingSymbol);

                    //GetFutureMargin
                    //Margins[] margins = null;
                    //HttpClient client = new HttpClient();
                    //HttpResponseMessage response = await client.GetAsync("https://api.kite.trade/margins/futures");

                    //if (response.IsSuccessStatusCode)
                    //{
                    //    string response1 = await response.Content.ReadAsStringAsync();
                    //    margins = JsonConvert.DeserializeObject<Margins[]>(response1);
                    //}

                    //Margin
                    //UserMarginsResponse userMarginsResponse = kite.GetMargins();
                    //if (userMarginsResponse.Equity.Available.Cash > Convert.ToDecimal(259868.75))
                    //{

                    //}

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
                    //   positionSettings.Target > 0 && ((future.Realised + future.Unrealised) / System.Math.Abs(futureCount) >= positionSettings.Target))

                    if (System.Math.Abs(futureCount) > 0 && !positionSettings.IsTargetAchieved && positionSettings.CMode != IndicatorMode.None &&
                   positionSettings.Target > 0 && ((future.Unrealised) / System.Math.Abs(futureCount) >= positionSettings.Target))
                    {
                        if (positionSettings.SquareOffTarget <= 0) positionSettings.SquareOffTarget = 1;
                        positionSettings.MaxPostion = positionSettings.MaxPostion - positionSettings.SquareOffTarget;

                        if (positionSettings.MaxPostion < 0) positionSettings.MaxPostion = 0;
                        positionSettings.IsTargetAchieved = true;
                    }

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

                    //Expiry Date - Square off
                    //DateTime indianDateTime = GetIndianDateTime();
                    //if (positionSettings.Expiry.ToString("yyyy-MM-dd") == Convert.ToDateTime(indianDateTime).ToString("yyyy-MM-dd"))
                    //{
                    //    if (indianDateTime.Hour > 15)
                    //    {
                    //        if (IsNegative) positionSettings.CMode = IndicatorMode.Buy;
                    //        if (!IsNegative) positionSettings.CMode = IndicatorMode.Sell;
                    //        //Update Active flag
                    //    }
                    //}

                    if (IndicatorMode.Buy == positionSettings.CMode)
                    {
                        int quant = (IsNegative) ? positionSettings.MaxPostion + System.Math.Abs(futureCount) : positionSettings.MaxPostion - futureCount;
                        if (futureCount == 0)
                            quant = positionSettings.MaxPostion;
                        if (quant > 0 && futureCount != 0)
                        {
                            quant = futureCount;
                        }
                        var offerPrice = quote.First().Value.Offers.First().Price;

                        if (quant > 0)
                        {
                            PlaceOrder(positionSettings.Exchange, positionSettings.TradingSymbol, Constants.TRANSACTION_TYPE_BUY, quant * positionSettings.LotSize, offerPrice, Constants.ORDER_TYPE_LIMIT, Constants.PRODUCT_NRML);
                            Log(positionSettings.Exchange, positionSettings.TradingSymbol, positionSettings.NewShortEMA, positionSettings.NewLongEMA, Convert.ToInt32(futureCount.ToString() + "11" + positionSettings.MaxPostion.ToString()), offerPrice, positionSettings.CMode, positionSettings);
                        }
                        if (quant < 0) //execute if maxPositions is 0 and futureCount is 1 (SquareOff)
                        {
                            var bidPrice = quote.First().Value.Bids.First().Price;
                            PlaceOrder(positionSettings.Exchange, positionSettings.TradingSymbol, Constants.TRANSACTION_TYPE_SELL, System.Math.Abs(quant) * positionSettings.LotSize, bidPrice, Constants.ORDER_TYPE_LIMIT, Constants.PRODUCT_NRML);
                            Log(positionSettings.Exchange, positionSettings.TradingSymbol, positionSettings.NewShortEMA, positionSettings.NewLongEMA, Convert.ToInt32(futureCount.ToString() + "22" + positionSettings.MaxPostion.ToString()), bidPrice, positionSettings.CMode, positionSettings);
                        }
                    }
                    else if (IndicatorMode.Sell == positionSettings.CMode)
                    {
                        int quant = positionSettings.MaxPostion + futureCount;
                        var bidPrice = quote.First().Value.Bids.First().Price;
                        if (quant != 0 && futureCount != 0)
                        {
                            quant = futureCount;
                        }

                        if (futureCount == 0)
                            quant = positionSettings.MaxPostion;

                        if (quant > 0)
                        {
                            PlaceOrder(positionSettings.Exchange, positionSettings.TradingSymbol, Constants.TRANSACTION_TYPE_SELL, quant * positionSettings.LotSize, bidPrice, Constants.ORDER_TYPE_LIMIT, Constants.PRODUCT_NRML);
                            Log(positionSettings.Exchange, positionSettings.TradingSymbol, positionSettings.NewShortEMA, positionSettings.NewLongEMA,
                                Convert.ToInt32(futureCount.ToString() + "33" + positionSettings.MaxPostion.ToString()), bidPrice, positionSettings.CMode, positionSettings);
                        }
                        if (quant < 0)
                        {
                            var offerPrice = quote.First().Value.Offers.First().Price;
                            PlaceOrder(positionSettings.Exchange, positionSettings.TradingSymbol, Constants.TRANSACTION_TYPE_BUY, System.Math.Abs(quant) * positionSettings.LotSize, offerPrice, Constants.ORDER_TYPE_LIMIT, Constants.PRODUCT_NRML);
                            Log(positionSettings.Exchange, positionSettings.TradingSymbol, positionSettings.NewShortEMA, positionSettings.NewLongEMA,
                                Convert.ToInt32(futureCount.ToString() + "44" + positionSettings.MaxPostion.ToString()), offerPrice, positionSettings.CMode, positionSettings);
                        }
                    }



                    UpdateDB(positionSettings);
                }
            }
            catch (Exception ex)
            {
                ExceptionLog(ex);
            }
            Thread.Sleep(2000);
        }

        private static void ExecuteTrade(PositionSettings positionSettings)
        {
            try
            {
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
                    //   positionSettings.Target > 0 && ((future.Realised + future.Unrealised) / System.Math.Abs(futureCount) >= positionSettings.Target))

                    if (System.Math.Abs(futureCount) > 0 && !positionSettings.IsTargetAchieved && positionSettings.CMode != IndicatorMode.None &&
                   positionSettings.Target > 0 && ((future.Unrealised) / System.Math.Abs(futureCount) >= positionSettings.Target))
                    {
                        if (positionSettings.SquareOffTarget <= 0) positionSettings.SquareOffTarget = 1;
                        positionSettings.MaxPostion = positionSettings.MaxPostion - positionSettings.SquareOffTarget;

                        if (positionSettings.MaxPostion < 0) positionSettings.MaxPostion = 0;
                        positionSettings.IsTargetAchieved = true;
                    }

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


                    if (IndicatorMode.Buy == positionSettings.CMode)
                    {

                        int quant = (IsNegative) ? positionSettings.MaxPostion + System.Math.Abs(futureCount) : positionSettings.MaxPostion - futureCount;
                        var offerPrice = quote.First().Value.Offers.First().Price;

                        if (quant > 0)
                        {
                            PlaceOrder(positionSettings.Exchange, positionSettings.TradingSymbol, Constants.TRANSACTION_TYPE_BUY, quant * positionSettings.LotSize, offerPrice, Constants.ORDER_TYPE_LIMIT, Constants.PRODUCT_NRML);
                            Log(positionSettings.Exchange, positionSettings.TradingSymbol, positionSettings.NewShortEMA, positionSettings.NewLongEMA, Convert.ToInt32(futureCount.ToString() + "11" + positionSettings.MaxPostion.ToString()), offerPrice, positionSettings.CMode, positionSettings);
                        }
                        if (quant < 0) //execute if maxPositions is 0 and futureCount is 1 (SquareOff)
                        {
                            var bidPrice = quote.First().Value.Bids.First().Price;
                            PlaceOrder(positionSettings.Exchange, positionSettings.TradingSymbol, Constants.TRANSACTION_TYPE_SELL, System.Math.Abs(quant) * positionSettings.LotSize, bidPrice, Constants.ORDER_TYPE_LIMIT, Constants.PRODUCT_NRML);
                            Log(positionSettings.Exchange, positionSettings.TradingSymbol, positionSettings.NewShortEMA, positionSettings.NewLongEMA, Convert.ToInt32(futureCount.ToString() + "22" + positionSettings.MaxPostion.ToString()), bidPrice, positionSettings.CMode, positionSettings);
                        }
                    }
                    else if (IndicatorMode.Sell == positionSettings.CMode)
                    {
                        int quant = positionSettings.MaxPostion + futureCount;
                        var bidPrice = quote.First().Value.Bids.First().Price;
                        if (quant > 0)
                        {
                            PlaceOrder(positionSettings.Exchange, positionSettings.TradingSymbol, Constants.TRANSACTION_TYPE_SELL, quant * positionSettings.LotSize, bidPrice, Constants.ORDER_TYPE_LIMIT, Constants.PRODUCT_NRML);
                            Log(positionSettings.Exchange, positionSettings.TradingSymbol, positionSettings.NewShortEMA, positionSettings.NewLongEMA,
                                Convert.ToInt32(futureCount.ToString() + "33" + positionSettings.MaxPostion.ToString()), bidPrice, positionSettings.CMode, positionSettings);
                        }
                        if (quant < 0)
                        {
                            var offerPrice = quote.First().Value.Offers.First().Price;
                            PlaceOrder(positionSettings.Exchange, positionSettings.TradingSymbol, Constants.TRANSACTION_TYPE_BUY, System.Math.Abs(quant) * positionSettings.LotSize, offerPrice, Constants.ORDER_TYPE_LIMIT, Constants.PRODUCT_NRML);
                            Log(positionSettings.Exchange, positionSettings.TradingSymbol, positionSettings.NewShortEMA, positionSettings.NewLongEMA,
                                Convert.ToInt32(futureCount.ToString() + "44" + positionSettings.MaxPostion.ToString()), offerPrice, positionSettings.CMode, positionSettings);
                        }
                    }



                    UpdateDB(positionSettings);
                }
            }
            catch (Exception ex)
            {
                ExceptionLog(ex);
            }
            Thread.Sleep(2000);
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

        private static void UpdateDB(PositionSettings positionSettings)
        {
            string connectionString = ConfigurationManager.AppSettings["KiteDB"];
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string commandText = "UPDATE [PostionSettings] SET [LongEMA] =" + positionSettings.NewLongEMA +
                                                                ", [ShortEMA]=" + positionSettings.NewShortEMA +
                                                                ", [TradeOnCrossOverUpdate]='" + positionSettings.TradeOnCrossOverUpdate +
                                                                "', [TradeOnCrossOver]='" + positionSettings.TradeOnCrossOver +
                                                                "', [FMode]='" + positionSettings.CurrentMode +
                                                                "', [StableMode]='" + positionSettings.StableMode +
                                                                "', [LastRecorded]='" + positionSettings.LastRecorded.ToString("yyyy-MM-dd HH:mm:ss") +
                                                                "', [IsTargetAchieved] = '" + positionSettings.IsTargetAchieved.ToString() +
                                                                "', [MaxPostion] = '" + positionSettings.MaxPostion +
                                                                "', [LastUpdateTime] = '" + GetIndianDateTime().ToString("yyyy-MM-dd HH:mm:ss") +
                                                                "' WHERE [TradingSymbol]='" + positionSettings.TradingSymbol +
                                                                "'";

                SqlCommand command = new SqlCommand(commandText, conn)
                {
                    CommandType = System.Data.CommandType.Text,
                };
                command.ExecuteNonQuery();
                conn.Close();
            }
        }

        private static void Log(string exchange, string tradingSymbol, decimal item1, decimal item2, int futureCount, decimal bidPrice, IndicatorMode currentMode, PositionSettings positionSettings)
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

                //kite.PlaceOrder(Exchange: exchange,
                //                            TradingSymbol: tradingSymbol,
                //                            TransactionType: tRANSACTION_TYPE,
                //                            Quantity: quantity,
                //                            Price: bidPrice,
                //                            OrderType: oRDER_TYPE, //Check this
                //                            Product: ProductType
                //                        );
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
            lock (thisLockGetQuote)
            {
                Thread.Sleep(1000);
                Dictionary<string, Quote> quote = new Dictionary<string, Quote>();
                quote = kite.GetQuote(InstrumentId: new string[] { exchange + ":" + tradingSymbol });
                return quote;
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
