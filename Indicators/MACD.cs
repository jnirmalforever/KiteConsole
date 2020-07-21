using KiteConnect;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;

namespace KiteConsole_v2
{
    public class MACD : IIndicator
    {
        public Position StartLooking(Settings positionSettings, Kite kite, List<Attributes> Attributes = null)
        {
            positionSettings.CMode = IndicatorMode.None;
            positionSettings.IsCrossOver = false;

            if (Attributes.Count > 0)
            {
                decimal multiplier = Decimal.Divide(2, (positionSettings.runSettings.MACDEMA1 + 1));
                decimal shortSMA = Decimal.Divide(Attributes.OrderBy(x => x.TimeStamp)
                                           .Take(positionSettings.runSettings.MACDEMA1).Sum(x => x.Close), positionSettings.runSettings.MACDEMA1);

                //Calculating the EMA: [Closing price-EMA (previous day)] x multiplier + EMA (previous day)

                foreach (Attributes att in Attributes)
                {
                    int i = Attributes.IndexOf(att);
                    if (i == positionSettings.runSettings.MACDEMA1)
                    {
                        att.MACDEMA1 = (att.Close - shortSMA) * multiplier + shortSMA;
                    }
                    else if (i > positionSettings.runSettings.MACDEMA1)
                    {
                        att.MACDEMA1 = (att.Close - Attributes[i - 1].MACDEMA1) * multiplier + Attributes[i - 1].MACDEMA1;
                    }
                }

                multiplier = Decimal.Divide(2, (positionSettings.runSettings.MACDEMA2 + 1));
                shortSMA = Decimal.Divide(Attributes.OrderBy(x => x.TimeStamp).Take(positionSettings.runSettings.MACDEMA2).Sum(x => x.Close), positionSettings.runSettings.MACDEMA2);

                //Calculating the EMA: [Closing price-EMA (previous day)] x multiplier + EMA (previous day)

                foreach (Attributes att in Attributes)
                {
                    int i = Attributes.IndexOf(att);
                    if (i == positionSettings.runSettings.MACDEMA2)
                    {
                        att.MACDEMA2 = (att.Close - shortSMA) * multiplier + shortSMA;
                    }
                    else if (i > positionSettings.runSettings.MACDEMA2)
                    {
                        att.MACDEMA2 = (att.Close - Attributes[i - 1].MACDEMA2) * multiplier + Attributes[i - 1].MACDEMA2;
                    }
                }


                foreach (Attributes att in Attributes)
                {
                    int i = Attributes.IndexOf(att);
                    if (i > positionSettings.runSettings.MACDEMA2)
                    {
                        att.MACD = att.MACDEMA1 - att.MACDEMA2;
                    }
                }

                multiplier = Decimal.Divide(2, (positionSettings.runSettings.MACDSignalLine + 1));
                shortSMA = Decimal.Divide(Attributes.OrderBy(x => x.TimeStamp).Skip(positionSettings.runSettings.MACDEMA2 + 1).Take(positionSettings.runSettings.MACDSignalLine).Sum(x => x.MACD), positionSettings.runSettings.MACDSignalLine);

                //Calculating the EMA: [Closing price-EMA (previous day)] x multiplier + EMA (previous day)

                foreach (Attributes att in Attributes)
                {
                    int i = Attributes.IndexOf(att);
                    if (i == positionSettings.runSettings.MACDSignalLine + positionSettings.runSettings.MACDEMA2)
                    {
                        att.SignalLine = (att.MACD - shortSMA) * multiplier + shortSMA;
                    }
                    else if (i > positionSettings.runSettings.MACDSignalLine + positionSettings.runSettings.MACDEMA2)
                    {
                        att.SignalLine = (att.MACD - Attributes[i - 1].SignalLine) * multiplier + Attributes[i - 1].SignalLine;
                    }
                }

                foreach (Attributes att in Attributes)
                {
                    int i = Attributes.IndexOf(att);
                    if (i > positionSettings.runSettings.MACDEMA2)
                    {
                        if (att.MACD > att.SignalLine)
                        {
                            att.MACDMode = IndicatorMode.Buy;
                        }
                        else
                        {
                            att.MACDMode = IndicatorMode.Sell;
                        }
                    }
                }

                return new Position()
                {
                    PositionAttributes = Attributes,
                    PositionSettings = positionSettings
                };
            }
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
                                                                                           //timeInIndia = Convert.ToDateTime("01-0positionSettings.runSettings.MACDSignalLine-2018 01:55:32.000");
            return timeInIndia;
        }

        public Settings StartLooking(Settings positionSettings, Kite kite)
        {
            throw new NotImplementedException();
        }
    }
}
