using KiteConnect;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;

namespace KiteConsole_v2
{
    public class STAttributes
    {
        public decimal Close { get; set; }
        public DateTime TimeStamp { get; set; }
        public decimal TrueRange { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal ATR { get; internal set; }
        public decimal basicUpperBand { get; internal set; }
        public decimal basicLowerBand { get; internal set; }
        public decimal finalUpperBand { get; internal set; }
        public decimal finalLowerBand { get; internal set; }
        public decimal ATR1 { get; internal set; }
        public IndicatorMode Mode { get; set; }
        public decimal PL { get; internal set; }
    }
    public class SuperTrend : IIndicator
    {
        public PositionSettings StartLooking(PositionSettings positionSettings, Kite kite)
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

                List<STAttributes> STAttributes = historical.OrderBy(x => x.TimeStamp).Select(x => new STAttributes
                {
                    Close = x.Close,
                    TimeStamp = x.TimeStamp,
                    High = x.High,
                    Low = x.Low,
                    Open = x.Open
                }).OrderBy(x => x.TimeStamp).ToList();


                //Calculating the EMA: [Closing price-EMA (previous day)] x multiplier + EMA (previous day)

                foreach (STAttributes att in STAttributes)
                {
                    int i = STAttributes.IndexOf(att);
                    if (i == 0)
                    {

                    }
                    else
                    {
                        decimal findMax = Math.Max((STAttributes[i].High - STAttributes[i].Low), Math.Abs(STAttributes[i].High - STAttributes[i - 1].Close));
                        att.TrueRange = Math.Max(Math.Abs(STAttributes[i].Low - STAttributes[i - 1].Close), findMax);
                    }
                    if (i == Constants.SuperTrend)
                    {
                        att.ATR = Decimal.Divide(STAttributes.OrderBy(x => x.TimeStamp).Skip(1).Take(Constants.SuperTrend).Sum(x => x.TrueRange), Constants.SuperTrend);
                    }
                }

                decimal multiplier = Decimal.Divide(2, (Constants.SuperTrend + 1));

                List<STAttributes> fSTAttributes = STAttributes.OrderBy(x => x.TimeStamp).Skip(Constants.SuperTrend).Select(x => new STAttributes
                {
                    Close = x.Close,
                    TimeStamp = x.TimeStamp,
                    High = x.High,
                    Low = x.Low,
                    Open = x.Open,
                    ATR = x.ATR,
                    TrueRange = x.TrueRange
                }).OrderBy(x => x.TimeStamp).ToList();

                //TrueRange EMA (Zerodha Chart)
                foreach (STAttributes att in fSTAttributes)
                {
                    int i = fSTAttributes.IndexOf(att);
                    if (i > 0)
                    {
                        att.ATR = (att.TrueRange - fSTAttributes[i - 1].ATR) * multiplier + fSTAttributes[i - 1].ATR;
                    }
                }
                //TrueRange Average (PI Chart)
                //foreach (STAttributes att in fSTAttributes)
                //{
                //    int i = fSTAttributes.IndexOf(att);
                //    att.ATR = Decimal.Divide(STAttributes.OrderBy(x => x.TimeStamp).Skip(i + 1).Take(Constants.SuperTrend).Sum(x => x.TrueRange), Constants.SuperTrend);
                //}

                //Current ATR = [(Prior ATR x 13) + Current TR] / 14
                //foreach (STAttributes att in fSTAttributes)
                //{
                //    int i = fSTAttributes.IndexOf(att);
                //    if (i > 0)
                //    {
                //        att.ATR = ((fSTAttributes[i - 1].ATR * Constants.SuperTrend - 1) + att.TrueRange) / Constants.SuperTrend;
                //    }
                //}

                //BASIC UPPERBAND = (HIGH + LOW) / 2 + Multiplier * ATR
                //BASIC LOWERBAND = (HIGH + LOW) / 2 - Multiplier * ATR
                foreach (STAttributes att in fSTAttributes)
                {
                    att.basicUpperBand = Decimal.Divide((att.High + att.Low), 2) + Decimal.Multiply(3, att.ATR);
                    att.basicLowerBand = Decimal.Divide((att.High + att.Low), 2) - Decimal.Multiply(3, att.ATR);
                }


                //FINAL UPPERBAND = IF((Current BASICUPPERBAND < Previous FINAL UPPERBAND) and(Previous Close > Previous FINAL UPPERBAND)) THEN(Current BASIC UPPERBAND) ELSE Previous FINALUPPERBAND)
                //FINAL LOWERBAND = IF((Current BASIC LOWERBAND > Previous FINAL LOWERBAND) and(Previous Close < Previous FINAL LOWERBAND)) THEN(Current BASIC LOWERBAND) ELSE Previous FINAL LOWERBAND)
                foreach (STAttributes att in fSTAttributes)
                {
                    int i = fSTAttributes.IndexOf(att);
                    if (i == 0)
                    {
                        att.finalLowerBand = att.basicLowerBand;
                        att.finalUpperBand = att.basicUpperBand;
                        continue;
                    }
                    if ((att.basicUpperBand < fSTAttributes[i - 1].finalUpperBand) || (fSTAttributes[i - 1].Close > fSTAttributes[i - 1].finalUpperBand))
                    {
                        att.finalUpperBand = att.basicUpperBand;
                    }
                    else
                    {
                        att.finalUpperBand = fSTAttributes[i - 1].finalUpperBand;
                    }

                    if ((att.basicLowerBand > fSTAttributes[i - 1].finalLowerBand) || (fSTAttributes[i - 1].Close < fSTAttributes[i - 1].finalLowerBand))
                    {
                        att.finalLowerBand = att.basicLowerBand;
                    }
                    else
                    {
                        att.finalLowerBand = fSTAttributes[i - 1].finalLowerBand;
                    }
                }

                positionSettings.NewLongEMA = 0;
                positionSettings.NewShortEMA = 0;

                foreach (STAttributes att in fSTAttributes)
                {
                    int i = fSTAttributes.IndexOf(att);
                    if (i == 0 && att.Close <= att.finalUpperBand)
                    {
                        att.Mode = IndicatorMode.Sell;
                        continue;
                    }
                    else if (i == 0)
                    {
                        att.Mode = IndicatorMode.Buy;
                        continue;
                    }

                    if (fSTAttributes[i - 1].Mode == IndicatorMode.Buy)
                    {
                        if (att.Close <= att.finalLowerBand)
                        {
                            att.Mode = IndicatorMode.Sell;
                        }
                        else
                        {
                            att.Mode = fSTAttributes[i - 1].Mode;
                        }
                    }
                    else if (fSTAttributes[i - 1].Mode == IndicatorMode.Sell)
                    {
                        if (att.Close <= att.finalUpperBand)
                        {
                            att.Mode = fSTAttributes[i - 1].Mode;
                        }
                        else
                        {
                            att.Mode = IndicatorMode.Buy;
                        }
                    }
                }

                if (positionSettings.Interval == Constants.INTERVAL_15MINUTE || positionSettings.Interval == Constants.INTERVAL_30MINUTE)
                {
                    positionSettings.CurrentMode = fSTAttributes[fSTAttributes.Count - 2].Mode;
                    positionSettings.CMode = fSTAttributes[fSTAttributes.Count - 2].Mode;
                }
                else
                {
                    positionSettings.CurrentMode = fSTAttributes[fSTAttributes.Count - 1].Mode;
                    positionSettings.CMode = fSTAttributes[fSTAttributes.Count - 1].Mode;
                }

                //if (positionSettings.CMode == IndicatorMode.Buy)
                //{
                //    positionSettings.NewLongEMA = fSTAttributes[fSTAttributes.Count - 1].finalLowerBand;
                //    positionSettings.NewShortEMA = fSTAttributes[fSTAttributes.Count - 1].Close;
                //}
                //else
                //{
                //    positionSettings.NewLongEMA = fSTAttributes[fSTAttributes.Count - 1].finalUpperBand;
                //    positionSettings.NewShortEMA = fSTAttributes[fSTAttributes.Count - 1].Close;
                //}

                if (positionSettings.Interval == Constants.INTERVAL_15MINUTE || positionSettings.Interval == Constants.INTERVAL_30MINUTE)
                {
                    if (positionSettings.CMode == IndicatorMode.Buy)
                    {
                        positionSettings.NewLongEMA = fSTAttributes[fSTAttributes.Count - 2].finalLowerBand;
                        positionSettings.NewShortEMA = fSTAttributes[fSTAttributes.Count - 1].Close;

                    }
                    else
                    {
                        positionSettings.NewLongEMA = fSTAttributes[fSTAttributes.Count - 2].finalUpperBand;
                        positionSettings.NewShortEMA = fSTAttributes[fSTAttributes.Count - 1].Close;
                    }
                }
                //Calculate PL
                var pls = fSTAttributes[0].Close;
                decimal currentPL = 00;
                foreach (STAttributes st in fSTAttributes)
                {
                    int i = fSTAttributes.IndexOf(st);

                    if (i > 0 && fSTAttributes[i].Mode != fSTAttributes[i - 1].Mode)
                    {
                        var ple = (fSTAttributes.Count - 1 == i) ? fSTAttributes[i].Open : fSTAttributes[i + 1].Open;
                        if (fSTAttributes[i - 1].Mode == IndicatorMode.Buy)
                        {
                            positionSettings.PL = Decimal.Subtract(ple, pls);
                            fSTAttributes[i].PL = Decimal.Subtract(ple, pls);

                        }
                        else
                        {
                            positionSettings.PL = Decimal.Subtract(pls, ple);
                            fSTAttributes[i].PL = Decimal.Subtract(pls, ple);
                        }

                        pls = (fSTAttributes.Count - 1 == i) ? fSTAttributes[i].Open : fSTAttributes[i + 1].Open;
                    }
                    if (fSTAttributes[fSTAttributes.Count - 1].Mode == IndicatorMode.Buy)
                        currentPL = Decimal.Subtract(fSTAttributes[fSTAttributes.Count - 1].Close, pls);
                    else if (fSTAttributes[fSTAttributes.Count - 1].Mode == IndicatorMode.Sell)
                        currentPL = Decimal.Subtract(pls, fSTAttributes[fSTAttributes.Count - 1].Close);
                }

                decimal totalPL = 0;
                foreach (STAttributes st in fSTAttributes)
                {
                    if (st.PL != 0)
                        totalPL += st.PL;
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

                //Remove false signals //Becz, we are using half hour delay already
                //if (positionSettings.CMode != IndicatorMode.None)
                //{
                //    positionSettings = CheckforStableMode(positionSettings);
                //}

                UpdateDB(positionSettings);
            }
            return positionSettings;
        }
        private static void Log(string exchange, string tradingSymbol, decimal item1, decimal item2, int futureCount, decimal bidPrice, IndicatorMode currentMode, PositionSettings positionSettings)
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

        private PositionSettings LogCrossOver(PositionSettings positionSettings)
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

        private PositionSettings CheckCrossOver(PositionSettings positionSettings)
        {
            if (positionSettings.CurrentMode != positionSettings.PMode)
            {
                positionSettings.TradeOnCrossOver = false;
                return positionSettings;
            }
            positionSettings.CMode = IndicatorMode.None;
            return positionSettings;
        }
        private PositionSettings CheckforStableMode(PositionSettings positionSettings)
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



        private void UpdateDB(PositionSettings positionSettings)
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
            //timeInIndia = Convert.ToDateTime("17-10-2018 10:55:32.000");
            return timeInIndia;
        }


    }
}
