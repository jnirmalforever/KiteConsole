using KiteConnect;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;

namespace KiteConsole_v2
{
    static class Extensions
    {
        public static IList<T> Clone<T>(this IList<T> listToClone) where T : ICloneable
        {
            return listToClone.Select(item => (T)item.Clone()).ToList();
        }
    }
    public class RSIv1 : IIndicator
    {
        private decimal Round(decimal input)
        {
            return Math.Round(input, 2);

        }
        public Position StartLooking(Settings positionSettings, Kite kite, List<Attributes> Attributes = null)
        {
            positionSettings.CMode = IndicatorMode.None;
            positionSettings.IsCrossOver = false;

            foreach (Attributes att in Attributes)
            {
                int i = Attributes.IndexOf(att);

                if (i > 0 && i < Attributes.Count)
                {
                    att.Change = Attributes[i].Close - Attributes[i - 1].Close;
                    att.Gain = att.Change > 0 ? att.Change : 0;
                    att.Loss = att.Change < 0 ? att.Change : 0;
                }
            }

            foreach (Attributes att in Attributes)
            {
                int i = Attributes.IndexOf(att);
                if (i == positionSettings.runSettings.RSIPeriod)
                //if (i == positionSettings.runSettings.RSIPeriod + 1)

                {
                    att.AverageGain = Round(Attributes.OrderBy(x => x.TimeStamp).Skip(i + 1 - positionSettings.runSettings.RSIPeriod).Take(positionSettings.runSettings.RSIPeriod).Average(x => x.Gain));
                    att.AverageLoss = Round(Attributes.OrderBy(x => x.TimeStamp).Skip(i + 1 - positionSettings.runSettings.RSIPeriod).Take(positionSettings.runSettings.RSIPeriod).Average(x => x.Loss));
                    if (att.AverageLoss != 0)
                        att.RS = Round(Decimal.Divide(att.AverageGain, att.AverageLoss));
                    else
                        att.RS = 0;
                    att.RSI = Round(100 - (100 / (1 + Math.Abs(att.RS))));

                }
                if (i > positionSettings.runSettings.RSIPeriod)
                {
                    att.AverageGain = Round(Decimal.Divide(Decimal.Add(decimal.Multiply(Attributes[i - 1].AverageGain, positionSettings.runSettings.RSILong - 1), att.Gain), positionSettings.runSettings.RSILong));
                    att.AverageLoss = Round(Decimal.Divide(Decimal.Add(decimal.Multiply(Attributes[i - 1].AverageLoss, positionSettings.runSettings.RSILong - 1), att.Loss), positionSettings.runSettings.RSILong));
                    if (att.AverageLoss != 0)
                        att.RS = Round(Decimal.Divide(att.AverageGain, att.AverageLoss));
                    else
                        att.RS = 0;
                    att.RSI = Round(100 - (100 / (1 + Math.Abs(att.RS))));

                }
            }
            //Reference
            //http://cns.bu.edu/~gsc/CN710/fincast/Technical%20_indicators/Relative%20Strength%20Index%20(RSI).htm

            decimal multiplier = Decimal.Divide(2, (positionSettings.runSettings.RSIShort + 1));
            decimal shortSMA = Decimal.Divide(Attributes.OrderBy(x => x.TimeStamp).Skip(positionSettings.runSettings.RSILong)
                                       .Take(positionSettings.runSettings.RSIShort).Sum(x => x.RSI), positionSettings.runSettings.RSIShort);

            //Calculating the EMA: [Closing price-EMA (previous day)] x multiplier + EMA (previous day)

            foreach (Attributes att in Attributes)
            {
                int i = Attributes.IndexOf(att);
                if (i == positionSettings.runSettings.RSIPeriod + positionSettings.runSettings.RSIShort - 1)
                {
                    att.RSIEMA1 = shortSMA;// (att.RSI - shortSMA) * multiplier + shortSMA;
                }
                else if (i > positionSettings.runSettings.RSIPeriod + positionSettings.runSettings.RSIShort - 1)
                {
                    att.RSIEMA1 = Round((att.RSI - Attributes[i - 1].RSIEMA1) * multiplier + Attributes[i - 1].RSIEMA1);
                }
            }

            multiplier = Decimal.Divide(2, (positionSettings.runSettings.RSILong + 1));
            shortSMA = Decimal.Divide(Attributes.OrderBy(x => x.TimeStamp).Skip(positionSettings.runSettings.RSILong)
                                      .Take(positionSettings.runSettings.RSILong).Sum(x => x.Momentum), positionSettings.runSettings.RSILong);

            //Calculating the EMA: [Closing price-EMA (previous day)] x multiplier + EMA (previous day)

            foreach (Attributes att in Attributes)
            {
                int i = Attributes.IndexOf(att);
                if (i == positionSettings.runSettings.RSIPeriod + positionSettings.runSettings.RSILong - 1)
                {
                    att.RSIEMA2 = shortSMA;// (att.RSI - shortSMA) * multiplier + shortSMA;
                }
                else if (i > positionSettings.runSettings.RSIPeriod + positionSettings.runSettings.RSILong - 1)
                {
                    att.RSIEMA2 = (att.RSI - Attributes[i - 1].RSIEMA2) * multiplier + Attributes[i - 1].RSIEMA2;
                }
            }

            foreach (Attributes att in Attributes)
            {
                int i = Attributes.IndexOf(att);
                if (i > positionSettings.runSettings.RSIPeriod + positionSettings.runSettings.RSILong)
                {
                    if (att.RSIEMA1 > att.RSIEMA2)
                    {
                        att.RSIMode = IndicatorMode.Buy;
                    }
                    else
                    {
                        att.RSIMode = IndicatorMode.Sell;
                    }
                }
            }

            return new Position()
            {
                PositionAttributes = Attributes,
                PositionSettings = positionSettings
            };

            //SuperTrend(positionSettings, kite, Attributes);
            //return positionSettings;


            //Improve, comment above rsimode before using below
            //foreach (Attributes att in Attributes)
            //{
            //    int i = Attributes.IndexOf(att);
            //    if (i > positionSettings.runSettings.RSIPeriod + positionSettings.runSettings.RSILong)
            //    {
            //        if (att.EMA1 > att.EMA2)
            //        {
            //            if (decimal.Subtract(att.EMA1, att.EMA2) < Convert.ToDecimal(1))
            //            {
            //                att.RSIMode = Attributes[Attributes.IndexOf(att) - 1].RSIMode;
            //                continue;
            //            }

            //            att.RSIMode = IndicatorMode.Buy;
            //        }
            //        else
            //        {
            //            if (decimal.Subtract(att.EMA2, att.EMA1) < Convert.ToDecimal(1))
            //            {
            //                att.RSIMode = Attributes[Attributes.IndexOf(att) - 1].RSIMode;
            //                continue;
            //            }

            //            att.RSIMode = IndicatorMode.Sell;
            //        }
            //    }
            //}


            foreach (Attributes att in Attributes)
            {
                int i = Attributes.IndexOf(att);

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

                if (Attributes[i - 1].STMode == IndicatorMode.Buy)
                {
                    if (att.Close <= att.finalLowerBand)
                    {
                        att.IsNewSFSignal = true;
                        att.STMode = IndicatorMode.Sell;
                    }
                    else
                    {
                        att.STMode = Attributes[i - 1].STMode;
                        att.IsNewSFSignal = false;

                    }
                }
                else if (Attributes[i - 1].STMode == IndicatorMode.Sell)
                {
                    if (att.Close <= att.finalUpperBand)
                    {
                        att.STMode = Attributes[i - 1].STMode;
                        att.IsNewSFSignal = false;

                    }
                    else
                    {
                        att.STMode = IndicatorMode.Buy;
                        att.IsNewSFSignal = true;

                    }
                }
            }



            // Calculate PL
            var pls = Attributes[29].Close;
            decimal currentPL = 00;
            foreach (Attributes st in Attributes)
            {
                int i = Attributes.IndexOf(st);

                if (i > 28 && Attributes[i].RSIMode != Attributes[i - 1].RSIMode)
                {
                    var ple = (Attributes.Count - 1 == i) ? Attributes[i].Open : Attributes[i + 1].Open;
                    if (Attributes[i - 1].RSIMode == IndicatorMode.Buy)
                    {
                        positionSettings.PL = Decimal.Subtract(ple, pls);
                        Attributes[i].PL = Decimal.Subtract(ple, pls);

                    }
                    else
                    {
                        positionSettings.PL = Decimal.Subtract(pls, ple);
                        Attributes[i].PL = Decimal.Subtract(pls, ple);
                    }
                    //if (positionSettings.PL == Convert.ToDecimal(173.50) || positionSettings.PL == Convert.ToDecimal(positionSettings.runSettings.RSIShort06.30))
                    //{

                    //}
                    pls = (Attributes.Count - 1 == i) ? Attributes[i].Open : Attributes[i + 1].Open;
                    //if (pls == 2250.95M)
                    //{

                    //}
                }
                if (Attributes[Attributes.Count - 1].RSIMode == IndicatorMode.Buy)
                    currentPL = Decimal.Subtract(Attributes[Attributes.Count - 1].Close, pls);
                else if (Attributes[Attributes.Count - 1].RSIMode == IndicatorMode.Sell)
                    currentPL = Decimal.Subtract(pls, Attributes[Attributes.Count - 1].Close);
            }



            decimal totalPL = 0;
            foreach (Attributes st in Attributes)
            {
                if (st.PL != 0)
                    totalPL += st.PL;
            }
            int count = 0;
            foreach (Attributes st in Attributes)
            {
                if (st.PL != 0)
                    count += 1;
            }
            totalPL = 0;
            count = 0;
            foreach (Attributes st in Attributes)
            {
                if (st.TimeStamp >= GetIndianDateTime().AddDays(-120))
                {

                    if (st.PL != 0)
                    {
                        totalPL += st.PL;
                        count += 1;
                    }
                }
            }

            if (positionSettings.Interval == Constants.INTERVAL_15MINUTE || positionSettings.Interval == Constants.INTERVAL_30MINUTE)
            {
                positionSettings.CurrentMode = Attributes[Attributes.Count - 1].STMode;
                positionSettings.CMode = Attributes[Attributes.Count - 1].STMode;
            }

            if (positionSettings.Interval == Constants.INTERVAL_15MINUTE || positionSettings.Interval == Constants.INTERVAL_30MINUTE)
            {
                //positionSettings.NewLongEMA = Attributes[Attributes.Count - 1].SAR;
                positionSettings.NewShortEMA = Attributes[Attributes.Count - 1].Close;
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
            return new Position()
            {
                PositionAttributes = Attributes,
                PositionSettings = positionSettings
            };
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
