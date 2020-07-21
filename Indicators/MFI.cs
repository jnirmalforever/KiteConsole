using KiteConnect;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;

namespace KiteConsole_v2
{
    public class MFI : IIndicator
    {
        private decimal Round(decimal input)
        {
            return Math.Round(input, 2);

        }
        public Position StartLooking(Settings positionSettings, Kite kite, List<Attributes> Attributes)
        {
            if (Attributes.Count > 0)
            {
                foreach (Attributes att in Attributes)
                {
                    int i = Attributes.IndexOf(att);

                    if (i == 0)
                    {
                        att.TypicalPrice = decimal.Divide(att.High + att.Low + att.Close, 3);
                        att.RawMoneyFlow = att.TypicalPrice * att.Volume;
                    }
                    if (i > 0)
                    {
                        att.TypicalPrice = decimal.Divide(att.High + att.Low + att.Close, 3);
                        att.PostiveMFI = att.TypicalPrice > Attributes[i - 1].TypicalPrice ? true : false;
                        att.RawMoneyFlow = att.TypicalPrice * att.Volume;
                    }
                    if (i > positionSettings.MFIPeriod - 1)
                    {
                        att.PositiveMoneyFlow = Attributes.OrderBy(x => x.TimeStamp).Skip(i - (positionSettings.MFIPeriod - 1)).Take(positionSettings.MFIPeriod).Where(x => x.PostiveMFI).Sum(x => x.RawMoneyFlow);
                        att.NegativeMoneyFlow = Attributes.OrderBy(x => x.TimeStamp).Skip(i - (positionSettings.MFIPeriod - 1)).Take(positionSettings.MFIPeriod).Where(x => !x.PostiveMFI).Sum(x => x.RawMoneyFlow);
                        att.MFR = (att.NegativeMoneyFlow == 0) ? 0 : decimal.Divide(att.PositiveMoneyFlow, att.NegativeMoneyFlow);
                        att.MFI = 100 - (100 / (1 + att.MFR));
                    }
                    if (i > positionSettings.MFIPeriod - 1 + positionSettings.runSettings.MFIEMA1)
                    {
                        decimal multiplier = Decimal.Divide(2, (positionSettings.runSettings.MFIEMA1 + 1));
                       
                        decimal shortSMA = Decimal.Divide(Attributes.OrderBy(x => x.TimeStamp).Skip(i - positionSettings.runSettings.MFIEMA1 + 1)
                                              .Take(positionSettings.runSettings.MFIEMA1).Sum(x => x.MFI), positionSettings.runSettings.MFIEMA1);

                        //var s = (Attributes[i].MFI + Attributes[i - 1].MFI + Attributes[i - 2].MFI + Attributes[i - 3].MFI + Attributes[i - 4].MFI) / 5;
                        //Calculating the EMA: [Closing price-EMA (previous day)] x multiplier + EMA (previous day)

                        if (i == positionSettings.MFIPeriod + positionSettings.runSettings.MFIEMA1)
                        {
                            att.MFIEMA1 = shortSMA;
                        }
                        else if (i > positionSettings.MFIPeriod + positionSettings.runSettings.MFIEMA1)
                        {
                            att.MFIEMA1 = Round((att.MFI - Attributes[i - 1].MFIEMA1) * multiplier + Attributes[i - 1].MFIEMA1);
                        }

                        //att.MFIEMA1 = shortSMA;

                    }
                }
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
            throw new System.NotImplementedException();
        }
    }
}