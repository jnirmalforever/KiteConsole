using KiteConnect;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;

namespace KiteConsole_v2
{
    public class Cloud : IIndicator
    {
        public Position StartLooking(Settings positionSettings, Kite kite, List<Attributes> Attributes = null)
        {
            positionSettings.CMode = IndicatorMode.None;
            positionSettings.IsCrossOver = false;

            if (Attributes.Count > 0)
            {

                // .Take(positionSettings.runSettings.MACDEMA1)
                decimal multiplier = Decimal.Divide(2, (positionSettings.runSettings.MACDEMA1 + 1));
                decimal shortSMA = Decimal.Divide(Attributes.OrderBy(x => x.TimeStamp)
                                           .Take(positionSettings.runSettings.MACDEMA1).Sum(x => x.Close), positionSettings.runSettings.MACDEMA1);


                //Calculating the EMA: [Closing price-EMA (previous day)] x multiplier + EMA (previous day)
                try
                {
                    foreach (Attributes att in Attributes)
                    {
                        if (att.Close == 2940)
                        {

                        }

                        int i = Attributes.IndexOf(att);
                        if (i >= 9)
                        {
                            att.ConverstionLine = Decimal.Divide(Attributes.OrderBy(x => x.TimeStamp).Skip(i + 1 - 9).Take(9).Max(x => x.High) +
                                                  Attributes.OrderBy(x => x.TimeStamp).Skip(i + 1 - 9).Take(9).Min(x => x.Low), 2);
                        }
                        if (i >= 26)
                        {
                            att.BaseLine = Decimal.Divide(Attributes.OrderBy(x => x.TimeStamp).Skip(i + 1 - 26).Take(26).Max(x => x.High) +
                                   Attributes.OrderBy(x => x.TimeStamp).Skip(i + 1 - 26).Take(26).Min(x => x.Low), 2);
                            att.LeadingSpanAStraight = (att.ConverstionLine + att.BaseLine) / 2;

                            //if (i + 26 < Attributes.Count)
                            //    Attributes[i + 26].LeadingSpanA = (att.ConverstionLine + att.BaseLine) / 2;
                            att.LeadingSpanA = Attributes[i - 26].LeadingSpanAStraight; //att.LeadingSpanAStraight - 26;
                        }
                        if (i >= 52)
                        {
                            att.LeadingSpanBStraight = Decimal.Divide(Attributes.OrderBy(x => x.TimeStamp).Skip(i + 1 - 52).Take(52).Max(x => x.High) +
                                   Attributes.OrderBy(x => x.TimeStamp).Skip(i + 1 - 52).Take(52).Min(x => x.Low), 2);

                            att.LeadingSpanB = Attributes[i - 26].LeadingSpanBStraight; //katt.LeadingSpanBStraight - 26;

                            //if (i + 22 < Attributes.Count) 
                            //    Attributes[i + 22].LeadingSpanB = LeadingSpanB;

                        }
                    }
                }
                catch (Exception ex)
                {

                }

                foreach (Attributes att in Attributes)
                {
                    int i = Attributes.IndexOf(att);
                    if (i > 105)
                    {
                        if (att.Close == 2359)
                        {

                        }
                        decimal pmaxLeadingSpan;
                        decimal pminLeadingSpan;
                        if (Attributes[i - 1].LeadingSpanA > Attributes[i - 1].LeadingSpanB)
                        {
                            pmaxLeadingSpan = Attributes[i - 1].LeadingSpanA;
                            pminLeadingSpan = Attributes[i - 1].LeadingSpanB;
                        }
                        else
                        {
                            pmaxLeadingSpan = Attributes[i - 1].LeadingSpanB;
                            pminLeadingSpan = Attributes[i - 1].LeadingSpanA;
                        }

                        if (Attributes[i].LeadingSpanA > Attributes[i].LeadingSpanB)
                        {
                            positionSettings.maxLeadingSpan = Attributes[i].LeadingSpanA;
                            positionSettings.minLeadingSpan = Attributes[i].LeadingSpanB;
                        }
                        else
                        {
                            positionSettings.maxLeadingSpan = Attributes[i].LeadingSpanB;
                            positionSettings.minLeadingSpan = Attributes[i].LeadingSpanA;
                        }

                        att.maxLeadingSpan = positionSettings.maxLeadingSpan;
                        att.minLeadingSpan = positionSettings.minLeadingSpan;

                        att.pmaxLeadingSpan = pmaxLeadingSpan;
                        att.pminLeadingSpan = pminLeadingSpan;

                        //if (att.ConverstionLine > att.BaseLine
                        //   && att.Close > att.LeadingSpanA && att.Close > att.LeadingSpanB && Attributes[i - 1].Close < pmaxLeadingSpan)
                        //{
                        //    att.CloudMode = IndicatorMode.Buy;
                        //    continue;
                        //}

                        if (att.Close > att.LeadingSpanA && att.Close > att.LeadingSpanB &&
                        Attributes[i - 1].Close < pmaxLeadingSpan)
                        {
                            positionSettings.BuyOnLeadingSpanA = true;
                            att.CloudMode = IndicatorMode.Buy;
                            continue;
                        }

                        if (positionSettings.BuyOnLeadingSpanA && att.Close > positionSettings.maxLeadingSpan)
                        {
                            att.CloudMode = IndicatorMode.Buy;
                            continue;
                        }


                        if ((att.Close < att.LeadingSpanA && att.Close < att.LeadingSpanB &&
                           Attributes[i - 1].Close > pminLeadingSpan))
                        {
                            positionSettings.SellOnLeadingSpanB = true;
                            att.CloudMode = IndicatorMode.Sell;
                            continue;
                        }

                        if (positionSettings.SellOnLeadingSpanB && att.Close < positionSettings.minLeadingSpan)
                        {
                            att.CloudMode = IndicatorMode.Sell;
                            continue;
                        }

                        if (Attributes[i - 1].CloudMode == IndicatorMode.Buy && Attributes[i].CloudMode == IndicatorMode.None)
                        {
                            Attributes[i].CloudMode = IndicatorMode.SellNWait;
                        }

                        if (Attributes[i - 1].CloudMode == IndicatorMode.Sell && Attributes[i].CloudMode == IndicatorMode.None)
                        {
                            Attributes[i].CloudMode = IndicatorMode.BuyNWait;
                        }

                        if (Attributes[i - 1].CloudMode == IndicatorMode.SellNWait)
                        {
                            Attributes[i].CloudMode = IndicatorMode.SellNWait;
                        }

                        if (Attributes[i - 1].CloudMode == IndicatorMode.BuyNWait)
                        {
                            Attributes[i].CloudMode = IndicatorMode.BuyNWait;
                        }

                        if (Attributes[i].CloudMode != Attributes[i - 1].CloudMode)
                        {

                        }

                    }
                }

                //foreach (Attributes att in Attributes)
                //{
                //    int i = Attributes.IndexOf(att);
                //    if (i > 105)
                //    {
                //        if (att.Close == 2359)
                //        {

                //        }
                //        decimal pmaxLeadingSpan;
                //        decimal pminLeadingSpan;
                //        if (Attributes[i - 1].LeadingSpanA > Attributes[i - 1].LeadingSpanB)
                //        {
                //            pmaxLeadingSpan = Attributes[i - 1].LeadingSpanA;
                //            pminLeadingSpan = Attributes[i - 1].LeadingSpanB;
                //        }
                //        else
                //        {
                //            pmaxLeadingSpan = Attributes[i - 1].LeadingSpanB;
                //            pminLeadingSpan = Attributes[i - 1].LeadingSpanA;
                //        }

                //        decimal maxLeadingSpan;
                //        decimal minLeadingSpan;
                //        if (Attributes[i].LeadingSpanA > Attributes[i].LeadingSpanB)
                //        {
                //            maxLeadingSpan = Attributes[i].LeadingSpanA;
                //            minLeadingSpan = Attributes[i].LeadingSpanB;
                //        }
                //        else
                //        {
                //            maxLeadingSpan = Attributes[i].LeadingSpanB;
                //            minLeadingSpan = Attributes[i].LeadingSpanA;
                //        }


                //        if (att.ConverstionLine > att.BaseLine
                //           && att.Close > att.LeadingSpanA && att.Close > att.LeadingSpanB && Attributes[i - 1].Close < pmaxLeadingSpan)
                //        {
                //            att.CloudMode = IndicatorMode.Buy;
                //            continue;
                //        }

                //        if (att.Close > att.LeadingSpanA && att.Close > att.LeadingSpanB &&
                //        Attributes[i - 1].Close < pmaxLeadingSpan)
                //        {
                //            positionSettings.BuyOnLeadingSpanA = true;
                //            att.CloudMode = IndicatorMode.Buy;
                //            continue;
                //        }

                //        if (Attributes[i - 1].CloudMode == IndicatorMode.Buy && Attributes[i].ConverstionLine > Attributes[i].BaseLine)// && Attributes[i].Close > pmaxLeadingSpan)
                //        {
                //            positionSettings.BuyOnLeadingSpanA = false;
                //            att.CloudMode = IndicatorMode.Buy;
                //            continue;
                //        }

                //        if (positionSettings.BuyOnLeadingSpanA && att.Close > maxLeadingSpan)
                //        {
                //            att.CloudMode = IndicatorMode.Buy;
                //            continue;
                //        }
                //        if (Attributes[i - 1].CloudMode == IndicatorMode.Sell && (Attributes[i].BaseLine > Attributes[i].ConverstionLine && Attributes[i].Close < pminLeadingSpan))
                //        {
                //            att.CloudMode = IndicatorMode.Sell;
                //            continue;
                //        }

                //        if (att.BaseLine > att.ConverstionLine
                //             && att.Close < att.LeadingSpanA && att.Close < att.LeadingSpanB && Attributes[i - 1].Close > pminLeadingSpan)
                //        {
                //            att.CloudMode = IndicatorMode.Sell;
                //            continue;
                //        }
                //        if (att.BaseLine > att.ConverstionLine
                //           && att.Close < att.LeadingSpanA && att.Close < att.LeadingSpanB)// && Attributes[i - 1].Close > pminLeadingSpan)
                //        {
                //            att.CloudMode = IndicatorMode.Sell;
                //            continue;
                //        }

                //        if ((att.Close < att.LeadingSpanA && att.Close < att.LeadingSpanB &&
                //           Attributes[i - 1].Close > pminLeadingSpan))
                //        {
                //            positionSettings.SellOnLeadingSpanB = true;
                //            att.CloudMode = IndicatorMode.Sell;
                //            continue;
                //        }

                //        if (Attributes[i - 1].CloudMode == IndicatorMode.Sell && Attributes[i].ConverstionLine < Attributes[i].BaseLine && Attributes[i].Close < pminLeadingSpan)
                //        {
                //            positionSettings.SellOnLeadingSpanB = false;
                //            att.CloudMode = IndicatorMode.Sell;
                //            continue;
                //        }

                //        if (positionSettings.SellOnLeadingSpanB && att.Close < minLeadingSpan)
                //        {
                //            att.CloudMode = IndicatorMode.Sell;
                //            continue;
                //        }

                //        if (Attributes[i - 1].CloudMode == IndicatorMode.Buy && Attributes[i].CloudMode == IndicatorMode.None)
                //        {
                //            Attributes[i].CloudMode = IndicatorMode.SellNWait;
                //        }

                //        if (Attributes[i - 1].CloudMode == IndicatorMode.Sell && Attributes[i].CloudMode == IndicatorMode.None)
                //        {
                //            Attributes[i].CloudMode = IndicatorMode.BuyNWait;
                //        }

                //        if (Attributes[i - 1].CloudMode == IndicatorMode.SellNWait)
                //        {
                //            Attributes[i].CloudMode = IndicatorMode.SellNWait;
                //        }

                //        if (Attributes[i - 1].CloudMode == IndicatorMode.BuyNWait)
                //        {
                //            Attributes[i].CloudMode = IndicatorMode.BuyNWait;
                //        }

                //        if (Attributes[i].CloudMode != Attributes[i - 1].CloudMode)
                //        {

                //        }

                //    }
                //}


                foreach (Attributes attributes in Attributes)
                {
                    int j = Attributes.IndexOf(attributes);
                    if (j > 105 && Attributes[j].CloudMode != Attributes[j - 1].CloudMode)
                    {
                        Console.WriteLine(attributes.Close + ":" + attributes.TimeStamp + ":" + attributes.CloudMode);

                    }
                }

                if (false)
                {
                    CalculatePL(new Position()
                    {
                        PositionAttributes = Attributes,
                        PositionSettings = positionSettings
                    });
                }

            }
            return new Position()
            {
                PositionAttributes = Attributes,
                PositionSettings = positionSettings
            };
        }

        public static void CalculatePL(Position position)
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
                if (i > 105 && Attributes[i].CloudMode != Attributes[i - 1].CloudMode && Attributes[i].CloudMode != IndicatorMode.None)
                {
                    if (Attributes[i - 1].CloudMode == IndicatorMode.None)
                    {
                        pls = Attributes[i].Close; continue;
                    }
                    decimal ple = 0;

                    if ((Attributes[i].CloudMode == IndicatorMode.SellNWait || Attributes[i].CloudMode == IndicatorMode.BuyNWait)
                        && (Attributes[i - 1].CloudMode == IndicatorMode.Sell || Attributes[i - 1].CloudMode == IndicatorMode.Buy))
                    {
                        ple = (Attributes.Count - 1 == i) ? Attributes[i].Open : Attributes[i + 1].Open;

                    }
                    else if ((Attributes[i - 1].CloudMode == IndicatorMode.SellNWait || Attributes[i - 1].CloudMode == IndicatorMode.BuyNWait)
                        && (Attributes[i].CloudMode == IndicatorMode.Sell || Attributes[i].CloudMode == IndicatorMode.Buy))
                    {
                        pls = Attributes[i].Close; continue;
                    }
                    else if (Attributes[i].CloudMode == IndicatorMode.Buy && Attributes[i - 1].CloudMode == IndicatorMode.Sell)
                    {
                        ple = (Attributes.Count - 1 == i) ? Attributes[i].Open : Attributes[i + 1].Open;
                    }
                    else if (Attributes[i - 1].CloudMode == IndicatorMode.Buy && Attributes[i].CloudMode == IndicatorMode.Sell)
                    {
                        ple = (Attributes.Count - 1 == i) ? Attributes[i].Open : Attributes[i + 1].Open;
                    }

                    TradeEndDateTime = (Attributes.Count - 1 == i) ? Attributes[i].TimeStamp : Attributes[i + 1].TimeStamp;
                    if (Attributes[i - 1].CloudMode == IndicatorMode.Buy)
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
                        Program.LogBackTest(position, pls, ple, Attributes[i], conn, totalTrades, totalPL, TradeStartDateTime, TradeEndDateTime, Attributes[i - 1].CloudMode);

                    if (Attributes[i].CloudMode != IndicatorMode.BuyNWait && Attributes[i].CloudMode != IndicatorMode.SellNWait)
                        pls = (Attributes.Count - 1 == i) ? Attributes[i].Open : Attributes[i + 1].Open;

                    TradeStartDateTime = (Attributes.Count - 1 == i) ? Attributes[i].TimeStamp : Attributes[i + 1].TimeStamp;
                }

            }

            if (Attributes[Attributes.Count - 1].CloudMode == IndicatorMode.Buy)
                currentPL = Decimal.Subtract(Attributes[Attributes.Count - 1].Close, pls);
            else if (Attributes[Attributes.Count - 1].CloudMode == IndicatorMode.Sell)
                currentPL = Decimal.Subtract(pls, Attributes[Attributes.Count - 1].Close);

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
                                                                                           //timeInIndia = Convert.ToDateTime("01-0positionSettings.runSettings.MACDSignalLine-2018 01:55:32.000");
            return timeInIndia;
        }

        public Settings StartLooking(Settings positionSettings, Kite kite)
        {
            throw new NotImplementedException();
        }

    }

}
