using KiteConnect;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;

namespace KiteConsole_v2
{
    //public class Attributes
    //{
    //    public decimal Close { get; set; }
    //    public DateTime TimeStamp { get; set; }
    //    public decimal TrueRange { get; set; }
    //    public decimal Open { get; set; }
    //    public decimal High { get; set; }
    //    public decimal Low { get; set; }
    //    public decimal ATR { get; internal set; }
    //    public decimal basicUpperBand { get; internal set; }
    //    public decimal basicLowerBand { get; internal set; }
    //    public decimal finalUpperBand { get; internal set; }
    //    public decimal finalLowerBand { get; internal set; }
    //    public decimal ATR1 { get; internal set; }
    //    public IndicatorMode Mode { get; set; }
    //    public decimal PL { get; internal set; }
    //}
    public class SuperTrend : IIndicator
    {
        public Position StartLooking(Settings positionSettings, Kite kite, List<Attributes> Attributes = null)
        {
            positionSettings.CMode = IndicatorMode.None;
            positionSettings.IsCrossOver = false;

            if (Attributes.Count > 0)
            {

                //Calculating the EMA: [Closing price-EMA (previous day)] x multiplier + EMA (previous day)
                foreach (Attributes att in Attributes)
                {
                    att.basicLowerBand = 0;
                    att.basicUpperBand = 0;
                    att.ATR = 0;
                    att.TrueRange = 0;
                    att.finalLowerBand = 0;
                    att.finalUpperBand = 0;
                }
                foreach (Attributes att in Attributes)
                {
                    int i = Attributes.IndexOf(att);
                    if (i == 0)
                    {

                    }
                    else
                    {
                        decimal findMax = Math.Max((Attributes[i].High - Attributes[i].Low), Math.Abs(Attributes[i].High - Attributes[i - 1].Close));
                        att.TrueRange = Math.Max(Math.Abs(Attributes[i].Low - Attributes[i - 1].Close), findMax);
                    }
                    if (i == positionSettings.runSettings.STPeriod)
                    {
                        att.ATR = Decimal.Divide(Attributes.OrderBy(x => x.TimeStamp).Skip(1).Take(positionSettings.runSettings.STPeriod).Sum(x => x.TrueRange), positionSettings.runSettings.STPeriod);
                    }
                }

                //decimal multiplier = Decimal.Divide(Convert.ToDecimal(Constants.STMultiplier), (positionSettings.runSettings.STPeriod + 1));

                //decimal multiplier = Decimal.Divide(Convert.ToDecimal(positionSettings.runSettings.STMultiplier), (positionSettings.runSettings.STPeriod + 1));
                //Changed to EMA
                decimal multiplier = Decimal.Divide(2, (positionSettings.runSettings.STPeriod + 1));

                //List<Attributes> fAttributes = Attributes.OrderBy(x => x.TimeStamp).Skip(positionSettings.runSettings.STPeriod).Select(x => new Attributes
                //{
                //    Close = x.Close,
                //    TimeStamp = x.TimeStamp,
                //    High = x.High,
                //    Low = x.Low,
                //    Open = x.Open,
                //    ATR = x.ATR,
                //    TrueRange = x.TrueRange
                //}).OrderBy(x => x.TimeStamp).ToList();

                List<Attributes> fAttributes = Attributes.OrderBy(x => x.TimeStamp).Skip(positionSettings.runSettings.STPeriod).ToList();

                //TrueRange EMA (Zerodha Chart)
                //foreach (Attributes att in fAttributes)
                //{
                //    int i = fAttributes.IndexOf(att);
                //    if (i > 0)
                //    {
                //        att.ATR = (att.TrueRange - fAttributes[i - 1].ATR) * multiplier + fAttributes[i - 1].ATR;
                //    }
                //}


               // TrueRange EMA(Zerodha Chart) -v2
                foreach (Attributes att in fAttributes)
                {
                    int i = fAttributes.IndexOf(att);
                    if (i > 0)
                    {
                        att.ATR = (att.TrueRange - fAttributes[i - 1].ATR) * multiplier + fAttributes[i - 1].ATR;
                    }
                }


                //TrueRange Average (PI Chart)
                //foreach (Attributes att in fAttributes)
                //{
                //    int i = fAttributes.IndexOf(att);
                //    att.ATR = Decimal.Divide(Attributes.OrderBy(x => x.TimeStamp).Skip(i + 1).Take(positionSettings.runSettings.STPeriod).Sum(x => x.TrueRange), positionSettings.runSettings.STPeriod);
                //}

                //Current ATR = [(Prior ATR x 13) + Current TR] / 14
                //foreach (Attributes att in fAttributes)
                //{
                //    int i = fAttributes.IndexOf(att);
                //    if (i > 0)
                //    {
                //        att.ATR = ((fAttributes[i - 1].ATR * positionSettings.runSettings.STPeriod - 1) + att.TrueRange) / positionSettings.runSettings.STPeriod;
                //    }
                //}

                //BASIC UPPERBAND = (HIGH + LOW) / 2 + Multiplier * ATR
                //BASIC LOWERBAND = (HIGH + LOW) / 2 - Multiplier * ATR
                foreach (Attributes att in fAttributes)
                {
                    //att.basicUpperBand = Decimal.Divide((att.High + att.Low), 2) + Decimal.Multiply(Convert.ToDecimal(Constants.STMultiplier), att.ATR);
                    //att.basicLowerBand = Decimal.Divide((att.High + att.Low), 2) - Decimal.Multiply(Convert.ToDecimal(Constants.STMultiplier), att.ATR);

                    //att.basicUpperBand = Decimal.Divide((att.High + att.Low), 2) + Decimal.Multiply(Convert.ToDecimal(positionSettings.IndicatorParmOne), att.ATR);
                    //att.basicLowerBand = Decimal.Divide((att.High + att.Low), 2) - Decimal.Multiply(Convert.ToDecimal(positionSettings.IndicatorParmOne), att.ATR);

                    att.basicUpperBand = Decimal.Divide((att.High + att.Low), 2) + Decimal.Multiply(Convert.ToDecimal(positionSettings.runSettings.STBasic), att.ATR);
                    att.basicLowerBand = Decimal.Divide((att.High + att.Low), 2) - Decimal.Multiply(Convert.ToDecimal(positionSettings.runSettings.STBasic), att.ATR);
                }


                //FINAL UPPERBAND = IF((Current BASICUPPERBAND < Previous FINAL UPPERBAND) and(Previous Close > Previous FINAL UPPERBAND)) THEN(Current BASIC UPPERBAND) ELSE Previous FINALUPPERBAND)
                //FINAL LOWERBAND = IF((Current BASIC LOWERBAND > Previous FINAL LOWERBAND) and(Previous Close < Previous FINAL LOWERBAND)) THEN(Current BASIC LOWERBAND) ELSE Previous FINAL LOWERBAND)
                foreach (Attributes att in fAttributes)
                {
                    int i = fAttributes.IndexOf(att);
                    if (i == 0)
                    {
                        att.finalLowerBand = att.basicLowerBand;
                        att.finalUpperBand = att.basicUpperBand;
                        continue;
                    }

                    if ((att.basicUpperBand < fAttributes[i - 1].finalUpperBand) || (fAttributes[i - 1].Close > fAttributes[i - 1].finalUpperBand))
                    {
                        att.finalUpperBand = att.basicUpperBand;
                    }
                    else
                    {
                        att.finalUpperBand = fAttributes[i - 1].finalUpperBand;
                    }

                    if ((att.basicLowerBand > fAttributes[i - 1].finalLowerBand) || (fAttributes[i - 1].Close < fAttributes[i - 1].finalLowerBand))
                    {
                        att.finalLowerBand = att.basicLowerBand;
                    }
                    else
                    {
                        att.finalLowerBand = fAttributes[i - 1].finalLowerBand;
                    }
                }

                positionSettings.NewLongEMA = 0;
                positionSettings.NewShortEMA = 0;

                //Reset PL
                foreach (Attributes att in fAttributes)
                {
                    if (att.PL != 0)
                        att.PL = 0;
                }

                foreach (Attributes att in Attributes)
                {
                    if (att.PL != 0)
                        att.PL = 0;
                }

                if (positionSettings.runSettings.STIteration == 3)
                {
                    //Reset Mode
                    foreach (Attributes att in fAttributes)
                    {
                        att.STMode3 = IndicatorMode.None;
                    }
                    foreach (Attributes att in Attributes)
                    {
                        att.STMode3 = IndicatorMode.None;
                    }

                    foreach (Attributes att in fAttributes)
                    {
                        int i = fAttributes.IndexOf(att);

                        if (i == 0 && att.Close <= att.finalUpperBand)
                        {
                            att.STMode3 = IndicatorMode.Sell;
                            continue;
                        }
                        else if (i == 0)
                        {
                            att.STMode3 = IndicatorMode.Buy;
                            continue;
                        }

                        if (fAttributes[i - 1].STMode3 == IndicatorMode.Buy)
                        {
                            if (att.Close <= att.finalLowerBand)
                            {
                                att.IsNewSFSignal3 = true;
                                att.STMode3 = IndicatorMode.Sell;
                            }
                            else
                            {
                                att.STMode3 = fAttributes[i - 1].STMode3;
                                att.IsNewSFSignal3 = false;

                            }
                        }
                        else if (fAttributes[i - 1].STMode3 == IndicatorMode.Sell)
                        {
                            if (att.Close <= att.finalUpperBand)
                            {
                                att.STMode3 = fAttributes[i - 1].STMode3;
                                att.IsNewSFSignal3 = false;

                            }
                            else
                            {
                                att.STMode3 = IndicatorMode.Buy;
                                att.IsNewSFSignal3 = true;

                            }
                        }
                    }
                }

                //else if (positionSettings.runSettings.STIteration == 2)
                //{
                //    //Reset Mode
                //    foreach (Attributes att in fAttributes)
                //    {
                //        att.STMode2 = IndicatorMode.None;
                //    }
                //    foreach (Attributes att in Attributes)
                //    {
                //        att.STMode2 = IndicatorMode.None;
                //    }

                //    foreach (Attributes att in fAttributes)
                //    {
                //        int i = fAttributes.IndexOf(att);

                //        if (i == 0 && att.Close <= att.finalUpperBand)
                //        {
                //            att.STMode2 = IndicatorMode.Sell;
                //            continue;
                //        }
                //        else if (i == 0)
                //        {
                //            att.STMode2 = IndicatorMode.Buy;
                //            continue;
                //        }

                //        if (fAttributes[i - 1].STMode2 == IndicatorMode.Buy)
                //        {
                //            if (att.Close <= att.finalLowerBand)
                //            {
                //                att.IsNewSFSignal2 = true;
                //                att.STMode2 = IndicatorMode.Sell;
                //            }
                //            else
                //            {
                //                att.STMode2 = fAttributes[i - 1].STMode2;
                //                att.IsNewSFSignal2 = false;

                //            }
                //        }
                //        else if (fAttributes[i - 1].STMode2 == IndicatorMode.Sell)
                //        {
                //            if (att.Close <= att.finalUpperBand)
                //            {
                //                att.STMode2 = fAttributes[i - 1].STMode2;
                //                att.IsNewSFSignal2 = false;

                //            }
                //            else
                //            {
                //                att.STMode2 = IndicatorMode.Buy;
                //                att.IsNewSFSignal2 = true;

                //            }
                //        }
                //    }
                //}

                else
                {
                    // Reset Mode

                    foreach (Attributes att in fAttributes)
                    {
                        att.STMode = IndicatorMode.None;
                    }
                    foreach (Attributes att in Attributes)
                    {
                        att.STMode = IndicatorMode.None;
                    }

                    foreach (Attributes att in fAttributes)
                    {
                        int i = fAttributes.IndexOf(att);

                        if (att.Close == 2418.10M)
                        {

                        }

                        if (i == 0 && att.Close <= att.finalUpperBand)
                        {
                            att.STMode = IndicatorMode.Sell;
                            continue;
                        }
                        else if (i == 0)
                        {
                            att.STMode = IndicatorMode.Buy;
                            continue;
                        }

                        if (fAttributes[i - 1].STMode == IndicatorMode.Buy)
                        {
                            if (att.Close <= att.finalLowerBand)
                            {
                                att.IsNewSFSignal = true;
                                att.STMode = IndicatorMode.Sell;
                            }
                            else
                            {
                                att.STMode = fAttributes[i - 1].STMode;
                                att.IsNewSFSignal = false;

                            }
                        }
                        else if (fAttributes[i - 1].STMode == IndicatorMode.Sell)
                        {
                            if (att.Close <= att.finalUpperBand)
                            {
                                att.STMode = fAttributes[i - 1].STMode;
                                att.IsNewSFSignal = false;

                            }
                            else
                            {
                                att.STMode = IndicatorMode.Buy;
                                att.IsNewSFSignal = true;

                            }
                        }
                    }
                }


                if (positionSettings.IsTestRun)
                {
                    CalculatePL(new Position()
                    {
                        PositionAttributes = Attributes,
                        PositionSettings = positionSettings
                    });
                }

                UpdateDB(positionSettings);
            }
            return new Position()
            {
                PositionAttributes = Attributes,
                PositionSettings = positionSettings
            };
        }

        private static void CalculatePL(Position position)
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
                if (i > 28 && Attributes[i].STMode != Attributes[i - 1].STMode)
                {
                    var ple = (Attributes.Count - 1 == i) ? Attributes[i].Open : Attributes[i + 1].Open;
                    TradeEndDateTime = (Attributes.Count - 1 == i) ? Attributes[i].TimeStamp : Attributes[i + 1].TimeStamp;
                    if (Attributes[i - 1].STMode == IndicatorMode.Buy)
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
                        Program.LogBackTest(position, pls, ple, Attributes[i], conn, totalTrades, totalPL, TradeStartDateTime, TradeEndDateTime, Attributes[i - 1].STMode);
                    pls = (Attributes.Count - 1 == i) ? Attributes[i].Open : Attributes[i + 1].Open;
                    TradeStartDateTime = (Attributes.Count - 1 == i) ? Attributes[i].TimeStamp : Attributes[i + 1].TimeStamp;
                }
                if (Attributes[Attributes.Count - 1].STMode == IndicatorMode.Buy)
                    currentPL = Decimal.Subtract(Attributes[Attributes.Count - 1].Close, pls);
                else if (Attributes[Attributes.Count - 1].STMode == IndicatorMode.Sell)
                    currentPL = Decimal.Subtract(pls, Attributes[Attributes.Count - 1].Close);
            }

            if (!position.PositionSettings.IsDepth)
                Program.LogBackTest(position, 0, 0, Attributes[0], conn, totalTrades, totalPL, TradeStartDateTime, TradeEndDateTime, Attributes[0].STMode);

            conn.Close();

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

        public Settings StartLooking(Settings positionSettings, Kite kite)
        {
            throw new NotImplementedException();
        }
    }
}
