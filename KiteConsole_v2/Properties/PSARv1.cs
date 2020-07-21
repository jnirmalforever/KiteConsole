using KiteConnect;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;

namespace KiteConsole_v2
{
   
    public class PSARv1 : IIndicator
    {
        public Settings StartLooking(Settings positionSettings, Kite kite)
        {
            positionSettings.CMode = IndicatorMode.None;
            positionSettings.IsCrossOver = false;

            List<Historical> historical;
            if (positionSettings.Interval == null)
            {
                positionSettings.Interval = Constants.INTERVAL_60MINUTE;
            }

            if (positionSettings.Interval == Constants.INTERVAL_30MINUTE || positionSettings.Interval == Constants.INTERVAL_15MINUTE)
            {
                historical = Program.GetHistoricalData(kite, positionSettings.InstrumentToken, GetIndianDateTime().AddDays(-180), GetIndianDateTime(), positionSettings.Interval, false);
            }
            else if (string.IsNullOrEmpty(positionSettings.ParentToken))
                historical = Program.GetHistoricalData(kite, positionSettings.InstrumentToken, GetIndianDateTime().AddDays(-300), GetIndianDateTime(), positionSettings.Interval, false);
            else
                historical = Program.GetHistoricalData(kite, positionSettings.ParentToken, GetIndianDateTime().AddDays(-300), GetIndianDateTime(), positionSettings.Interval, false);


            if (historical.Count > 0)
            {
                //decimal longSMA = Decimal.Divide(historical.OrderBy(x => x.TimeStamp).Take(Constants.longEMA).Sum(x => x.Close), Constants.longEMA);
                //decimal shortSMA = Decimal.Divide(historical.OrderBy(x => x.TimeStamp).Skip(Constants.longEMA - Constants.shortEMA)
                //                            .Take(Constants.shortEMA).Sum(x => x.Close), Constants.shortEMA);

                List<PSARAttributes> PSARAttributes = historical.OrderBy(x => x.TimeStamp).Select(x => new PSARAttributes
                {
                    Close = x.Close,
                    TimeStamp = x.TimeStamp,
                    High = x.High,
                    Low = x.Low,
                    Open = x.Open
                }).OrderBy(x => x.TimeStamp).ToList();


                //Calculating the EMA: [Closing price-EMA (previous day)] x multiplier + EMA (previous day)
                //SAR5 - SAR4  + AF(H4-SAR4)

                bool GoShort = true;
                int skipValue = 0;
                foreach (PSARAttributes att in PSARAttributes)
                {
                    int i = PSARAttributes.IndexOf(att);
                    if (i == 0)
                    {
                        //Go Short   
                        att.EP = att.High;
                        att.Mode = IndicatorMode.Sell;
                        //Go Long   
                        //att.EP = att.Low;
                        //att.Mode = IndicatorMode.Buy;
                    }

                    else
                    {
                        if (PSARAttributes[i - 1].Mode == IndicatorMode.Sell)
                        {
                            if (i == 1)
                            {
                                att.SAR = PSARAttributes[0].EP;
                                att.EP = PSARAttributes.OrderBy(x => x.TimeStamp).Take(i + 1).Min(x => x.Low);
                                att.AF = Constants.minimumAF;
                                att.Mode = IndicatorMode.Sell;
                            }

                            //Check Yesterday SAR in Today's Range
                            if (i != 1 && PSARAttributes[i - 1].SAR < PSARAttributes[i - 1].Close)
                            {
                                att.Mode = IndicatorMode.Buy;
                                att.SAR = PSARAttributes[i - 1].EP;
                                att.EP = att.High;
                                att.AF = Constants.minimumAF;
                                skipValue = i;
                            }
                            else if (i != 1)
                            {
                                att.Mode = IndicatorMode.Sell;

                                if (skipValue != 0)
                                    att.EP = PSARAttributes.OrderBy(x => x.TimeStamp).Skip(skipValue).Take(i + 1 - skipValue).Min(x => x.Low);
                                else
                                    att.EP = PSARAttributes.OrderBy(x => x.TimeStamp).Take(i + 1).Min(x => x.Low);

                                if (att.EP < PSARAttributes[i - 1].EP)
                                    att.AF = (PSARAttributes[i - 1].AF == Constants.maximumAF) ? PSARAttributes[i - 1].AF : PSARAttributes[i - 1].AF + Constants.minimumAF;
                                else
                                    att.AF = PSARAttributes[i - 1].AF;


                                att.SAR = PSARAttributes[i - 1].SAR + PSARAttributes[i - 1].AF * (PSARAttributes[i - 1].EP - PSARAttributes[i - 1].SAR);
                                //Never move the SAR for tomorrow into the previous day's high or today's high
                                //if (i - 1 >= 0 && att.SAR < PSARAttributes[i - 1].High)
                                //{
                                //    att.SAR = PSARAttributes[i - 1].High;
                                //}
                                //if (i - 2 >= 0 && att.SAR < PSARAttributes[i - 2].High)
                                //{
                                //    att.SAR = PSARAttributes[i - 2].High;
                                //}
                                //Check Toda's SAR in Today's Range
                                if (att.High == 11631 && att.Open == 11614)
                                {

                                }
                                if (i != 1 && att.SAR < PSARAttributes[i - 1].Close)
                                {
                                    att.Mode = IndicatorMode.Buy;
                                    att.SAR = PSARAttributes[i - 1].EP; //att.EP;
                                    att.EP = att.High;
                                    att.AF = Constants.minimumAF;
                                    skipValue = i;
                                }
                            }
                        }
                        else if (PSARAttributes[i - 1].Mode == IndicatorMode.Buy)
                        {
                            if (i == 1)
                            {
                                att.SAR = PSARAttributes[0].EP;
                                att.EP = PSARAttributes.OrderBy(x => x.TimeStamp).Take(i + 1).Max(x => x.High);
                                att.AF = Constants.minimumAF;
                                att.Mode = IndicatorMode.Buy;
                            }
                            //Check Yesterday SAR in Today's Range
                            if (i != 1 && PSARAttributes[i - 1].SAR > PSARAttributes[i - 1].Close)
                            {
                                att.Mode = IndicatorMode.Sell;
                                att.SAR = PSARAttributes[i - 1].EP;
                                att.EP = att.Low;
                                att.AF = Constants.minimumAF;
                                skipValue = i;
                            }
                            else if (i != 1)
                            {

                                if (skipValue != 0)
                                    att.EP = PSARAttributes.OrderBy(x => x.TimeStamp).Skip(skipValue).Take(i + 1 - skipValue).Max(x => x.High);
                                else
                                    att.EP = PSARAttributes.OrderBy(x => x.TimeStamp).Take(i + 1).Max(x => x.High);

                                if (att.EP > PSARAttributes[i - 1].EP)
                                    att.AF = (PSARAttributes[i - 1].AF == Constants.maximumAF) ? PSARAttributes[i - 1].AF : PSARAttributes[i - 1].AF + Constants.minimumAF;
                                else
                                    att.AF = PSARAttributes[i - 1].AF;


                                att.SAR = PSARAttributes[i - 1].SAR + PSARAttributes[i - 1].AF * (PSARAttributes[i - 1].EP - PSARAttributes[i - 1].SAR);

                                //Never move the SAR for tomorrow into the previous day's low or today's low
                                //if (i - 1 >= 0 && att.SAR > PSARAttributes[i - 1].Low)
                                //{
                                //    att.SAR = PSARAttributes[i - 1].Low;
                                //}
                                //if (i - 2 >= 0 && att.SAR > PSARAttributes[i - 2].Low)
                                //{
                                //    att.SAR = PSARAttributes[i - 2].Low;
                                //}
                                att.Mode = IndicatorMode.Buy;
                                //Check Today's SAR in Today's Range
                                if (i != 1 && att.SAR > PSARAttributes[i - 1].Close)
                                {
                                    att.Mode = IndicatorMode.Sell;
                                    att.SAR = att.EP;
                                    att.EP = att.Low;
                                    att.AF = Constants.minimumAF;
                                    skipValue = i;
                                }
                            }

                        }
                    }
                }
                //Calculate PL
                var pls = PSARAttributes[1].SAR;
                decimal currentPL = 00;
                foreach (PSARAttributes st in PSARAttributes)
                {
                    int i = PSARAttributes.IndexOf(st);

                    if (i > 0 && PSARAttributes[i].Mode != PSARAttributes[i - 1].Mode)
                    {
                        var ple = PSARAttributes[i - 1].SAR;
                        if (PSARAttributes[i - 1].Mode == IndicatorMode.Buy)
                        {
                            positionSettings.PL = Decimal.Subtract(ple, pls);
                            PSARAttributes[i].PL = Decimal.Subtract(ple, pls);

                        }
                        else
                        {
                            positionSettings.PL = Decimal.Subtract(pls, ple);
                            PSARAttributes[i].PL = Decimal.Subtract(pls, ple);
                        }
                        pls = ple;
                    }
                    if (PSARAttributes[PSARAttributes.Count - 1].Mode == IndicatorMode.Buy)
                        currentPL = Decimal.Subtract(PSARAttributes[PSARAttributes.Count - 1].Close, pls);
                    else if (PSARAttributes[PSARAttributes.Count - 1].Mode == IndicatorMode.Sell)
                        currentPL = Decimal.Subtract(pls, PSARAttributes[PSARAttributes.Count - 1].Close);
                }

                decimal totalPL = 0;
                foreach (PSARAttributes st in PSARAttributes)
                {
                    if (st.PL != 0)
                        totalPL += st.PL;
                }
                int count = 0;
                foreach (PSARAttributes st in PSARAttributes)
                {
                    if (st.PL != 0)
                        count += 1;
                }
                totalPL = 0;
                foreach (PSARAttributes st in PSARAttributes)
                {
                    if (st.TimeStamp >= GetIndianDateTime().AddDays(-30))
                    {

                        if (st.PL != 0)
                            totalPL += st.PL;
                    }
                }

                if (positionSettings.Interval == Constants.INTERVAL_15MINUTE || positionSettings.Interval == Constants.INTERVAL_30MINUTE)
                {
                    positionSettings.CurrentMode = PSARAttributes[PSARAttributes.Count - 1].Mode;
                    positionSettings.CMode = PSARAttributes[PSARAttributes.Count - 1].Mode;
                }

                if (positionSettings.Interval == Constants.INTERVAL_15MINUTE || positionSettings.Interval == Constants.INTERVAL_30MINUTE)
                {
                    positionSettings.NewLongEMA = PSARAttributes[PSARAttributes.Count - 1].SAR;
                    positionSettings.NewShortEMA = PSARAttributes[PSARAttributes.Count - 1].Close;
                }


                if (positionSettings.TradeOnCrossOver == true)
                    positionSettings = CheckCrossOver(positionSettings);

                positionSettings = LogCrossOver(positionSettings); //Log Crossover

                if (positionSettings.TradeOnCrossOverUpdate == true)
                {
                    positionSettings.CMode = IndicatorMode.None;
                    positionSettings.TradeOnCrossOver = true;
                    positionSettings.TradeOnCrossOverUpdate = false;
                }

                UpdateDB(positionSettings);
            }
            return positionSettings;
        }
        private static void Log(string exchange, string tradingSymbol, decimal item1, decimal item2, int futureCount, decimal bidPrice, IndicatorMode currentMode, Settings positionSettings)
        {
            string connectionString = ConfigurationManager.AppSettings["KiteDB"];
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string currentSettings = JsonConvert.SerializeObject(positionSettings);
                SqlCommand command = new SqlCommand("INSERT INTO [LogSettings]([Exchange],[TradingSymbol],[Item1],[Item2],[BidPrice],[Mode]" +
                    ",[Count],CreateTime,Json) SELECT '" + exchange + "', '" + tradingSymbol + "', " + item1 + ", " + item2 + ", " + bidPrice + ",'" +
                    currentMode.ToString() + "', " + futureCount + ",'" + GetIndianDateTime().ToString("yyyy-MM-dd HH:mm:ss") + "','" + currentSettings + "'", conn);

                command.CommandType = System.Data.CommandType.Text;
                command.ExecuteNonQuery();
                conn.Close();
            }
        }

        private Settings LogCrossOver(Settings positionSettings)
        {
            if (positionSettings.CurrentMode != positionSettings.PMode)
            {
                positionSettings.TradeOnCrossOver = false;
                positionSettings.IsCrossOver = true;
                positionSettings.LastCrossOverTime = GetIndianDateTime();
                Log(positionSettings.Exchange + " CrossOverLog", positionSettings.TradingSymbol, positionSettings.NewShortEMA, positionSettings.NewLongEMA, 0, 0, positionSettings.CMode, positionSettings);
            }
            return positionSettings;
        }

        private Settings CheckCrossOver(Settings positionSettings)
        {
            if (positionSettings.CurrentMode != positionSettings.PMode)
            {
                positionSettings.TradeOnCrossOver = false;
                return positionSettings;
            }
            positionSettings.CMode = IndicatorMode.None;
            return positionSettings;
        }
        private Settings CheckforStableMode(Settings positionSettings)
        {
            if (positionSettings.StableTime == 0)
                positionSettings.StableTime = 30;

            if (positionSettings.PMode == IndicatorMode.None)
            {
                positionSettings.LastRecorded = GetIndianDateTime().AddMinutes(-positionSettings.StableTime);
                Log(positionSettings.Exchange + " CrossOverLog0", positionSettings.TradingSymbol, positionSettings.NewShortEMA,
                    positionSettings.NewLongEMA, 0, 0, positionSettings.CMode, positionSettings);
                return positionSettings;
            }

            if (positionSettings.CMode != positionSettings.PMode)
            {
                if (GetIndianDateTime() < positionSettings.LastRecorded.AddMinutes(positionSettings.StableTime))
                {
                    //False signal
                    positionSettings.LastRecorded = GetIndianDateTime();
                    positionSettings.CMode = IndicatorMode.None;
                    Log(positionSettings.Exchange + " CrossOverLog1", positionSettings.TradingSymbol, positionSettings.NewShortEMA,
                        positionSettings.NewLongEMA, 0, 0, positionSettings.CMode, positionSettings);

                    return positionSettings;
                }
                else
                {
                    //Stable Mode for first order
                    positionSettings.StableMode = positionSettings.CMode;
                    positionSettings.LastRecorded = GetIndianDateTime();
                    Log(positionSettings.Exchange + " CrossOverLog2", positionSettings.TradingSymbol, positionSettings.NewShortEMA, positionSettings.NewLongEMA,
                        0, 0, positionSettings.CMode, positionSettings);
                    return positionSettings;
                }
            }

            if (positionSettings.CMode == positionSettings.PMode)
            {
                if (GetIndianDateTime() < positionSettings.LastRecorded.AddMinutes(positionSettings.StableTime))
                {
                    //Wait for Stable Mode for second order onwards
                    positionSettings.CMode = IndicatorMode.None;
                    Log(positionSettings.Exchange + " CrossOverLog3", positionSettings.TradingSymbol, positionSettings.NewShortEMA, positionSettings.NewLongEMA,
                       0, 0, positionSettings.CMode, positionSettings);
                    //positionSettings.CMode = positionSettings.StableMode;
                }
            }
            return positionSettings;
        }



        private void UpdateDB(Settings positionSettings)
        {
            string connectionString = ConfigurationManager.AppSettings["KiteDB"];
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string commandText = "UPDATE [PostionSettings] SET [LongEMA] =" + positionSettings.NewLongEMA +
                                                                ", [ShortEMA]=" + positionSettings.NewShortEMA +
                                                                ",[TradeOnCrossOverUpdate]='" + positionSettings.TradeOnCrossOverUpdate +
                                                                "',[TradeOnCrossOver]='" + positionSettings.TradeOnCrossOver +
                                                                "',[FMode]='" + positionSettings.CurrentMode +
                                                                "',[StableMode]='" + positionSettings.StableMode +
                                                                "',[LastRecorded]='" + positionSettings.LastRecorded.ToString("yyyy-MM-dd HH:mm:ss") +
                                                                "',LastCrossOverTime='" + positionSettings.LastCrossOverTime.ToString("yyyy-MM-dd HH:mm:ss") +
                                                                "',[LastUpdateTime] = '" + GetIndianDateTime().ToString("yyyy-MM-dd HH:mm:ss") +
                                                                "' WHERE [TradingSymbol]='" + positionSettings.TradingSymbol + "'";

                SqlCommand command = new SqlCommand(commandText, conn);
                command.CommandType = System.Data.CommandType.Text;
                command.ExecuteNonQuery();
                conn.Close();
            }
        }
        public static DateTime GetIndianDateTime()
        {
            var indianZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            var timeInIndia = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, indianZone);//.ToString("yyyy-MM-dd HH:mm:ss");
            //timeInIndia = Convert.ToDateTime("01-09-2018 01:55:32.000");
            return timeInIndia;
        }

        public Position StartLooking(Settings positionSettings, Kite kite, List<Attributes> Attributes)
        {
            throw new NotImplementedException();
        }
    }
}

