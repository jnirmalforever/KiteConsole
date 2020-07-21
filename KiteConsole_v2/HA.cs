using KiteConnect;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;

namespace KiteConsole_v2
{
    public class HA : IIndicator
    {
        public Position StartLooking(Settings positionSettings, Kite kite, List<Attributes> Attributes)
        {

            foreach (Attributes att in Attributes)
            {
                int i = Attributes.IndexOf(att);
                if (i == 0)
                {
                    att.HAClose = decimal.Divide(att.Open + att.High + att.Low + att.Close, 4);
                    att.HAOpen = decimal.Divide(Attributes[i].Open + Attributes[i].Close, 2);
                    att.HAHigh = att.High;
                    att.HALow = att.Low;

                }
                if (i > 0)
                {
                    att.HAClose = decimal.Divide(att.Open + att.High + att.Low + att.Close, 4);
                    att.HAOpen = decimal.Divide(Attributes[i - 1].HAOpen + Attributes[i - 1].HAClose, 2);
                    att.HAHigh = new[] { att.High, att.HAOpen, att.HAClose }.Max();
                    att.HALow = new[] { att.Low, att.HAOpen, att.HAClose }.Min();

                    if (att.HAClose > att.HAOpen && att.MFI > att.MFIEMA1)
                        att.HACandle = HACandle.Green;
                    else if (att.HAClose < att.HAOpen && att.MFI < att.MFIEMA1)
                        att.HACandle = HACandle.Red;
                    else
                        att.HACandle = Attributes[i - 1].HACandle;


                    if (att.HAClose > att.HAOpen && att.MFI > att.MFIEMA1)
                        att.HACandle = HACandle.Green;
                    else if (att.HAClose < att.HAOpen && att.MFI < att.MFIEMA1)
                        att.HACandle = HACandle.Red;
                    else
                        att.HACandle = Attributes[i - 1].HACandle;

                    if (att.HACandle == HACandle.None)
                    {

                    }

                    if (att.HACandle == HACandle.None)
                    {

                    }
                }
            }

            if (true)
            {
                CalculatePL(new Position()
                {
                    PositionAttributes = Attributes,
                    PositionSettings = positionSettings
                });
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
            var plsHA = Attributes.Count > 28 ? Attributes[29].HAClose : 0;
            decimal currentPL = 00;
            int totalTrades = 0;
            DateTime TradeStartDateTime = DateTime.Today, TradeEndDateTime = DateTime.Today;
            decimal totalPL = 00;
            foreach (Attributes att in Attributes)
            {
                int i = Attributes.IndexOf(att);
                int count = 1;
                if (i > 0 && Attributes[i].HACandle != Attributes[i - 1].HACandle)
                {
                    var ple = (Attributes.Count - 1 == i) ? Attributes[i].Open : Attributes[i + 1].Open;
                    var pleHA = (Attributes.Count - 1 == i) ? Attributes[i].HAOpen : Attributes[i + 1].HAOpen;
                    TradeEndDateTime = (Attributes.Count - 1 == i) ? Attributes[i].TimeStamp : Attributes[i + 1].TimeStamp;
                    if (Attributes[i - 1].HACandle == HACandle.Green)
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
                    position.PositionSettings.TradingSymbol = plsHA + " : " + pleHA;
                    if (position.PositionSettings.IsDepth)
                        Program.LogBackTest(position, pls, ple, Attributes[i], conn, totalTrades, totalPL, TradeStartDateTime, TradeEndDateTime, Attributes[i - 1].HACandle);
                    pls = (Attributes.Count - 1 == i) ? Attributes[i].Open : Attributes[i + 1].Open;
                    plsHA = (Attributes.Count - 1 == i) ? Attributes[i].HAOpen : Attributes[i + 1].HAOpen;

                    TradeStartDateTime = (Attributes.Count - 1 == i) ? Attributes[i].TimeStamp : Attributes[i + 1].TimeStamp;
                }
                if (Attributes[Attributes.Count - 1].HACandle == HACandle.Green)
                    currentPL = Decimal.Subtract(Attributes[Attributes.Count - 1].Close, pls);
                else if (Attributes[Attributes.Count - 1].HACandle == HACandle.Red)
                    currentPL = Decimal.Subtract(pls, Attributes[Attributes.Count - 1].Close);
            }

            if (!position.PositionSettings.IsDepth)
                Program.LogBackTest(position, 0, 0, Attributes[0], conn, totalTrades, totalPL, TradeStartDateTime, TradeEndDateTime, Attributes[0].STMode);

            conn.Close();

        }

        public Settings StartLooking(Settings positionSettings, Kite kite)
        {
            throw new System.NotImplementedException();
        }

    }
}
