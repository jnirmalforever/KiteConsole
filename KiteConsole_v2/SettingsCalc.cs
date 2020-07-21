using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;

namespace KiteConsole_v2
{
    public class SettingsCalc
    {
        static IIndicator reqIndicator = null;
        static RunSettings bestRunSettings = new RunSettings();
        public static void IndicatorCheck_v1(Settings positionSettings)
        {
            //positionSettings.IsTestRun = true;
            List<Attributes> attributes = null;
            positionSettings.ParentInstrument = "81153";

            attributes = DAL.GetDataFromDB(positionSettings.ParentInstrument);

            positionSettings.IsMACDEnabled = true;
            positionSettings.IsSTEnabled = true;
            positionSettings.IsRSIEnabled = true;

            Position position = new Position()
            {
                PositionAttributes = attributes,
                PositionSettings = positionSettings
            };

            if (!positionSettings.IsAdxEnabled)
            {
                reqIndicator = new ADX();
                position = reqIndicator.StartLooking(position.PositionSettings, null, position.PositionAttributes);
            }

            if (positionSettings.IsSTEnabled)
            {
                position.PositionSettings.runSettings.STMultiplier = 1.5M;
                position.PositionSettings.runSettings.STBasic = 1.5M;
                position.PositionSettings.runSettings.STPeriod = 14;
                PrepareSTPSettings(position);
            }

            if (positionSettings.IsRSIEnabled)
            {
                ///PrepareRSISettings(position);
                reqIndicator = new RSIv1();
                position = reqIndicator.StartLooking(position.PositionSettings, null, position.PositionAttributes);
            }

            if (positionSettings.IsMACDEnabled)
            {
                PrepareMACDSettings(position);
            }

            position.PositionSettings.MacdNoise = 2;
            Analysis_v6(position, 0 * 30, position.PositionSettings.MacdNoise);

            //Calculate PL
            CalculatePL(position);
        }

        public static void PrepareRSISettings(Position position)
        {
            throw new NotImplementedException();
        }

        private static void PrepareSTPSettings(Position position)
        {

            reqIndicator = new SuperTrend();
            position = reqIndicator.StartLooking(position.PositionSettings, null, position.PositionAttributes);
            return;

            RunSettings runSettings = position.PositionSettings.runSettings;

            for (int samplesCount = runSettings.STMinPeriod; samplesCount <= runSettings.STMaxPeriod; samplesCount++)
            {
                for (Decimal multiplyFactor = runSettings.STMinMultiplier; multiplyFactor <= runSettings.STMaxMultiplier; multiplyFactor = multiplyFactor + 0.1M)
                {
                    position.PositionSettings.runSettings.STBasic = multiplyFactor;
                    position.PositionSettings.runSettings.STMultiplier = multiplyFactor;
                    position.PositionSettings.runSettings.STPeriod = samplesCount;

                    reqIndicator = new SuperTrend();
                    position = reqIndicator.StartLooking(position.PositionSettings, null, position.PositionAttributes);

                    Analysis_v6(position, 0 * 30, position.PositionSettings.MacdNoise);

                    CalculatePL(position);
                    Console.WriteLine(string.Format("SuperTrend: {0} | Multiplier: {1} | STPeriod: {2}", bestRunSettings.Points, multiplyFactor, samplesCount));
                    if (bestRunSettings.Points < runSettings.CurrentPoints && runSettings.Trades < 150)
                    {
                        UpdateRunSettings(position);
                    }
                }
            }
            Console.ReadLine();
        }
        private static void PrepareMACDSettings(Position position)
        {
            reqIndicator = new MACD();
            position = reqIndicator.StartLooking(position.PositionSettings, null, position.PositionAttributes);
            return;
            for (Int32 fastSamples = 6; fastSamples <= 30; fastSamples++)
            {
                for (Int32 slowSamples = (fastSamples + 12); (slowSamples <= (fastSamples + 20)); slowSamples++)
                {
                    for (Int32 signalSample = 6; signalSample <= 20; signalSample++)
                    {
                        reqIndicator = new MACD();
                        position = reqIndicator.StartLooking(position.PositionSettings, null, position.PositionAttributes);
                    }
                }
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
                    var ple = (Attributes.Count - 1 == i) ? Attributes[i].Open : Attributes[i + 1].Open;
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
                    if (position.PositionSettings.IsDepth)
                        Program.LogBackTest(position, pls, ple, Attributes[i], conn, totalTrades, totalPL, TradeStartDateTime, TradeEndDateTime, IndicatorMode.None);
                    pls = (Attributes.Count - 1 == i) ? Attributes[i].Open : Attributes[i + 1].Open;
                    TradeStartDateTime = (Attributes.Count - 1 == i) ? Attributes[i].TimeStamp : Attributes[i + 1].TimeStamp;

                }
                if (Attributes[Attributes.Count - 1].Mode == IndicatorMode.Buy)
                    currentPL = Decimal.Subtract(Attributes[Attributes.Count - 1].Close, pls);
                else if (Attributes[Attributes.Count - 1].Mode == IndicatorMode.Sell)
                    currentPL = Decimal.Subtract(pls, Attributes[Attributes.Count - 1].Close);
            }

            //if (!position.PositionSettings.IsDepth)
            //    Program.LogBackTest(position, 0, 0, Attributes[0], conn, totalTrades, totalPL, TradeStartDateTime, TradeEndDateTime, Attributes[0].Mode);

            position.PositionSettings.runSettings.CurrentPoints = totalPL;
            position.PositionSettings.runSettings.Trades = totalTrades;
            conn.Close();
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
                    //Test
                    if (att.TimeStamp > Program.GetIndianDateTime().AddDays(-1))
                    {

                    }
                    //
                    if (att.Open == 896.70M)
                    {

                    }
                    if (att.Open == 991M)
                    {

                    }
                    //if (att.Adx <= 20)
                    //{
                    //    att.Mode = fAttributes[i - 1].Mode; //IndicatorMode.Sell;
                    //    continue;
                    //}
                    if (att.STMode == IndicatorMode.Buy)
                    {
                        if (position.PositionSettings.BuyRSITriggered || position.PositionSettings.BuyRSITrigger)
                        {
                            position.PositionSettings.BuyRSITrigger = false;
                            position.PositionSettings.BuyRSITriggered = false;
                        }
                        if (position.PositionSettings.BuyMACDTriggered || position.PositionSettings.BuyMACDTrigger)
                        {
                            position.PositionSettings.BuyMACDTrigger = false;
                            position.PositionSettings.BuyMACDTriggered = false;
                        }

                        if (Buy(att.STMode) && Buy(att.RSIMode) && Buy(att.MACDMode) && Buy(att.AdxMode) && CustomRule1(att))
                        {
                            att.Mode = IndicatorMode.Buy;
                        }

                        else if (Buy(att.STMode) && Buy(att.RSIMode) && Buy(att.MACDMode) && CustomRule2(att, position) && CustomRule201(att))
                        {
                            att.Mode = IndicatorMode.Buy;
                        }

                        else if (Buy(att.STMode) && Sell(att.RSIMode) && Sell(att.MACDMode) && CustomRule3(att, position))
                        {
                            att.EarlyBird = true;
                            att.Mode = IndicatorMode.Sell;
                        }

                        else if (Buy(att.STMode) && Sell(att.RSIMode) && Sell(att.MACDMode) && fAttributes[i - 1].EarlyBird == true)
                        {
                            att.EarlyBird = true;
                            att.Mode = IndicatorMode.Sell;
                        }
                        else
                            att.Mode = fAttributes[i - 1].Mode;


                        //if (position.PositionSettings.SellRSITriggered && position.PositionSettings.SellRSITrigger)
                        //{
                        //    if (att.Mode == IndicatorMode.Buy && att.MACDMode == IndicatorMode.Buy && att.RSIEMA1 > att.RSIEMA2)
                        //    {
                        //        position.PositionSettings.SellRSITrigger = false;
                        //        position.PositionSettings.SellRSITriggered = false;
                        //    }
                        //}
                        if (false)
                        {
                            if (!position.PositionSettings.SellRSITriggered && position.PositionSettings.SellRSITrigger && position.PositionSettings.Rule5
                                && att.RSIEMA1 < att.RSIEMA2)
                            {
                                att.Mode = IndicatorMode.Sell;
                                position.PositionSettings.SellRSITriggered = true;
                            }
                            if (!position.PositionSettings.SellRSITrigger && position.PositionSettings.Rule5 && att.STMode == IndicatorMode.Buy && att.RSI > 80 && att.Mode == IndicatorMode.Buy)
                            {
                                position.PositionSettings.SellRSITrigger = true;
                            }

                            if (!position.PositionSettings.SellRSITriggered && position.PositionSettings.SellRSITrigger && position.PositionSettings.Rule5
                               && att.RSIEMA1 > att.RSIEMA2)
                            {
                                //att.Mode = IndicatorMode.Sell;
                                position.PositionSettings.SellRSITriggered = false;
                            }


                            //if (!position.PositionSettings.SellMACDTriggered && position.PositionSettings.SellMACDTrigger && position.PositionSettings.Rule5 && att.SignalLine < att.MACD)
                            //{
                            //    att.Mode = IndicatorMode.Sell;
                            //    position.PositionSettings.SellMACDTriggered = true;
                            //}
                            //if (!position.PositionSettings.SellMACDTrigger && position.PositionSettings.Rule5 && att.MACDMode == IndicatorMode.Buy && att.MACD > 60 && att.Mode == IndicatorMode.Buy)
                            //{
                            //    position.PositionSettings.SellMACDTrigger = true;
                            //}

                            //if (position.PositionSettings.SellMACDTriggered)
                            //{
                            //    att.Mode = IndicatorMode.Sell;
                            //}

                            if (position.PositionSettings.SellRSITriggered)
                            {
                                att.Mode = IndicatorMode.Sell;
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


                        if (att.STMode == IndicatorMode.Sell && att.RSIMode == IndicatorMode.Sell && att.MACDMode == IndicatorMode.Sell && Math.Abs(decimal.Subtract(att.MACD, att.SignalLine)) >= macdNoise)
                        {

                            att.Mode = IndicatorMode.Sell;
                        }
                        //if (Sell(att.STMode) && Sell(att.RSIMode) && Sell(att.MACDMode) && Sell(att.AdxMode) && CustomRule4(att, position, i))
                        //{
                        //    att.Mode = IndicatorMode.Sell;
                        //}

                        else if (Sell(att.STMode) && Sell(att.RSIMode) && Sell(att.MACDMode) && CustomRule5(att, position) && CustomRule501(att, position))
                        {
                            att.Mode = IndicatorMode.Sell;
                        }

                        else if (Sell(att.STMode) && Buy(att.RSIMode) && Buy(att.MACDMode) && CustomRule6(att, position))
                        {
                            att.EarlyBird = true;
                            att.Mode = IndicatorMode.Buy;
                        }

                        else if (Sell(att.STMode) && Buy(att.RSIMode) && Buy(att.MACDMode) && fAttributes[i - 1].EarlyBird == true)
                        {
                            att.EarlyBird = true;
                            att.Mode = IndicatorMode.Buy;
                        }

                        else if (Sell(att.STMode) && Sell(att.RSIMode) && Sell(att.MACDMode) && CustomRule5(att, position))
                        {
                            att.Mode = IndicatorMode.Sell;
                        }

                        else if (Sell(att.STMode) && Sell(att.RSIMode) && Buy(att.MACDMode) && Sell(att.AdxMode) && CustomRule7(att, position, i))
                        {
                            att.Mode = IndicatorMode.Sell;
                        }
                        else
                            att.Mode = fAttributes[i - 1].Mode;


                        if (false)
                        {
                            if (!position.PositionSettings.BuyRSITriggered && position.PositionSettings.BuyRSITrigger && position.PositionSettings.Rule5
                                && att.RSIEMA1 > att.RSIEMA2)
                            {
                                att.Mode = IndicatorMode.Buy;
                                position.PositionSettings.BuyRSITriggered = true;
                            }
                            if (!position.PositionSettings.BuyRSITrigger && position.PositionSettings.Rule5 && att.STMode == IndicatorMode.Sell && att.RSI < 15 && att.Mode == IndicatorMode.Sell)
                            {
                                position.PositionSettings.BuyRSITrigger = true;
                            }

                            if (!position.PositionSettings.BuyMACDTriggered && position.PositionSettings.BuyMACDTrigger && position.PositionSettings.Rule5 && att.MACD > att.SignalLine)
                            {
                                att.Mode = IndicatorMode.Buy;
                                position.PositionSettings.BuyMACDTriggered = true;
                            }
                            if (!position.PositionSettings.BuyMACDTrigger && position.PositionSettings.Rule5 && att.MACDMode == IndicatorMode.Sell && att.MACD < -70 && att.Mode == IndicatorMode.Sell)
                            {
                                position.PositionSettings.BuyMACDTrigger = true;
                            }

                            if (position.PositionSettings.BuyMACDTriggered)
                            {
                                att.Mode = IndicatorMode.Buy;
                            }
                            if (position.PositionSettings.BuyRSITriggered)
                            {
                                att.Mode = IndicatorMode.Buy;
                            }
                        }
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

        private static bool CustomRule7(Attributes att, Position position, int i)
        {
            return (att.MACDMode == IndicatorMode.None || (att.MACDMode == IndicatorMode.Buy && Math.Abs(decimal.Subtract(position.PositionAttributes[i - 1].MACD, position.PositionAttributes[i - 1].SignalLine)) >= position.PositionSettings.MacdNoise));
        }

        private static bool CustomRule6(Attributes att, Position position)
        {
            return (att.MACDMode == IndicatorMode.None || (att.MACDMode == IndicatorMode.Buy && Math.Abs(decimal.Subtract(att.MACD, att.SignalLine)) >= 3));
        }

        private static bool CustomRule501(Attributes att, Position position)
        {
            return ((att.STMode == IndicatorMode.Sell && att.IsNewSFSignal) || att.STMode == IndicatorMode.None);
        }

        private static bool CustomRule5(Attributes att, Position position)
        {
            return (att.MACDMode == IndicatorMode.None || (att.MACDMode == IndicatorMode.Sell && Math.Abs(decimal.Subtract(att.MACD, att.SignalLine)) >= position.PositionSettings.MacdNoise));
        }

        private static bool CustomRule4(Attributes att, Position position, int i)
        {
            if (att.AdxMode == IndicatorMode.None || (position.PositionAttributes[i - 1].AdxMode == IndicatorMode.Buy || position.PositionAttributes[i - 2].AdxMode == IndicatorMode.Buy))
            {
                return (att.AdxMode == IndicatorMode.None || (att.AdxMode == IndicatorMode.Sell && Math.Abs(decimal.Subtract(att.PostiveDIn, att.NegativeDIn)) >= 7));
            }
            return false;
        }

        private static bool CustomRule3(Attributes att, Position position)
        {
            return (att.MACDMode == IndicatorMode.None || (att.MACDMode == IndicatorMode.Sell && Math.Abs(decimal.Subtract(att.MACD, att.SignalLine)) > 7));
        }

        private static bool CustomRule201(Attributes att)
        {
            return ((att.STMode == IndicatorMode.Buy && att.IsNewSFSignal) || att.STMode == IndicatorMode.None);
        }

        private static bool CustomRule2(Attributes att, Position position)
        {
            return (att.MACDMode == IndicatorMode.None || (att.MACDMode == IndicatorMode.Buy && Math.Abs(decimal.Subtract(att.MACD, att.SignalLine)) >= position.PositionSettings.MacdNoise));
        }

        private static bool CustomRule1(Attributes att)
        {
            return (att.AdxMode == IndicatorMode.None || (att.AdxMode == IndicatorMode.Buy && Math.Abs(decimal.Subtract(att.PostiveDIn, att.NegativeDIn)) >= 7));
        }

        private static bool Buy(IndicatorMode sTMode)
        {
            return (sTMode == IndicatorMode.Buy || sTMode == IndicatorMode.None);
        }
        private static bool Sell(IndicatorMode sTMode)
        {
            return (sTMode == IndicatorMode.Sell || sTMode == IndicatorMode.None);
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
                    if (att.TimeStamp > Program.GetIndianDateTime().AddDays(-1))
                    {

                    }
                    //
                    if (att.Open == 896.70M)
                    {

                    }
                    if (att.Open == 991M)
                    {

                    }
                    //if (att.Adx <= 20)
                    //{
                    //    att.Mode = fAttributes[i - 1].Mode; //IndicatorMode.Sell;
                    //    continue;
                    //}
                    if (att.STMode == IndicatorMode.Buy)
                    {
                        if (position.PositionSettings.BuyRSITriggered || position.PositionSettings.BuyRSITrigger)
                        {
                            position.PositionSettings.BuyRSITrigger = false;
                            position.PositionSettings.BuyRSITriggered = false;
                        }
                        if (position.PositionSettings.BuyMACDTriggered || position.PositionSettings.BuyMACDTrigger)
                        {
                            position.PositionSettings.BuyMACDTrigger = false;
                            position.PositionSettings.BuyMACDTriggered = false;
                        }
                        if (att.IsNewSFSignal && att.RSIMode == IndicatorMode.Buy && att.MACDMode == IndicatorMode.Buy && att.AdxMode == IndicatorMode.Buy)// && Math.Abs(decimal.Subtract(att.MACD, att.SignalLine)) < macdNoise)
                        {

                        }

                        //if (att.IsNewSFSignal && att.RSIMode == IndicatorMode.Buy && att.MACDMode == IndicatorMode.Buy && att.AdxMode == IndicatorMode.Buy)
                        if (position.PositionSettings.Rule1 && att.STMode == IndicatorMode.Buy && att.RSIMode == IndicatorMode.Buy && att.MACDMode == IndicatorMode.Buy && att.AdxMode == IndicatorMode.Buy && Math.Abs(decimal.Subtract(att.PostiveDIn, att.NegativeDIn)) >= 7)
                        {
                            att.Mode = IndicatorMode.Buy;
                        }
                        else if (position.PositionSettings.Rule2 && att.IsNewSFSignal && att.STMode == IndicatorMode.Buy && att.RSIMode == IndicatorMode.Buy && att.MACDMode == IndicatorMode.Buy && Math.Abs(decimal.Subtract(att.MACD, att.SignalLine)) >= macdNoise)
                        //if (att.RSIMode == IndicatorMode.Buy && att.MACDMode == IndicatorMode.Buy && att.AdxMode == IndicatorMode.Buy)// && Math.Abs(decimal.Subtract(att.PostiveDIn, att.NegativeDIn)) >= macdNoise)
                        {
                            att.Mode = IndicatorMode.Buy;
                        }
                        else if (position.PositionSettings.Rule3 && att.STMode == IndicatorMode.Buy && att.RSIMode == IndicatorMode.Sell && att.MACDMode == IndicatorMode.Sell && Math.Abs(decimal.Subtract(att.MACD, att.SignalLine)) > 7)
                        {
                            att.EarlyBird = true;
                            att.Mode = IndicatorMode.Sell;
                        }
                        else if (position.PositionSettings.Rule4 && att.STMode == IndicatorMode.Buy && fAttributes[i - 1].EarlyBird == true && att.RSIMode == IndicatorMode.Sell && att.MACDMode == IndicatorMode.Sell)
                        {
                            att.EarlyBird = true;
                            att.Mode = IndicatorMode.Sell;
                        }
                        else if (position.PositionSettings.Rule5 && att.STMode == IndicatorMode.Buy && att.RSIMode == IndicatorMode.Buy && att.MACDMode == IndicatorMode.Buy && Math.Abs(decimal.Subtract(att.MACD, att.SignalLine)) >= macdNoise)
                        //if (att.RSIMode == IndicatorMode.Buy && att.MACDMode == IndicatorMode.Buy && att.AdxMode == IndicatorMode.Buy)// && Math.Abs(decimal.Subtract(att.PostiveDIn, att.NegativeDIn)) >= macdNoise)
                        {

                            att.Mode = IndicatorMode.Buy;
                        }
                        //new
                        //else if (position.PositionSettings.Rule10 && att.STMode == IndicatorMode.Buy && att.RSIMode == IndicatorMode.Buy && att.AdxMode == IndicatorMode.Buy && att.MACDMode == IndicatorMode.Sell && Math.Abs(decimal.Subtract(fAttributes[i - 1].MACD, fAttributes[i - 1].SignalLine)) >= macdNoise)

                        //{
                        //    att.IsRSIValid = true;
                        //    att.IsMACDValid = true;
                        //    att.Mode = IndicatorMode.Sell;
                        //}
                        //
                        else
                            att.Mode = fAttributes[i - 1].Mode; //IndicatorMode.Sell;




                        //if (position.PositionSettings.SellRSITriggered && position.PositionSettings.SellRSITrigger)
                        //{
                        //    if (att.Mode == IndicatorMode.Buy && att.MACDMode == IndicatorMode.Buy && att.RSIEMA1 > att.RSIEMA2)
                        //    {
                        //        position.PositionSettings.SellRSITrigger = false;
                        //        position.PositionSettings.SellRSITriggered = false;
                        //    }
                        //}
                        if (false)
                        {
                            if (!position.PositionSettings.SellRSITriggered && position.PositionSettings.SellRSITrigger && position.PositionSettings.Rule5
                                && att.RSIEMA1 < att.RSIEMA2)
                            {
                                att.Mode = IndicatorMode.Sell;
                                position.PositionSettings.SellRSITriggered = true;
                            }
                            if (!position.PositionSettings.SellRSITrigger && position.PositionSettings.Rule5 && att.STMode == IndicatorMode.Buy && att.RSI > 90 && att.Mode == IndicatorMode.Buy)
                            {
                                position.PositionSettings.SellRSITrigger = true;
                            }



                            if (!position.PositionSettings.SellMACDTriggered && position.PositionSettings.SellMACDTrigger && position.PositionSettings.Rule5 && att.SignalLine < att.MACD)
                            {
                                att.Mode = IndicatorMode.Sell;
                                position.PositionSettings.SellMACDTriggered = true;
                            }
                            if (!position.PositionSettings.SellMACDTrigger && position.PositionSettings.Rule5 && att.MACDMode == IndicatorMode.Buy && att.MACD > 60 && att.Mode == IndicatorMode.Buy)
                            {
                                position.PositionSettings.SellMACDTrigger = true;
                            }

                            if (position.PositionSettings.SellMACDTriggered)
                            {
                                att.Mode = IndicatorMode.Sell;
                            }

                            if (position.PositionSettings.SellRSITriggered)
                            {
                                att.Mode = IndicatorMode.Sell;
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
                        if (att.IsNewSFSignal && att.RSIMode == IndicatorMode.Sell && att.MACDMode == IndicatorMode.Sell && Math.Abs(decimal.Subtract(att.MACD, att.SignalLine)) < macdNoise)
                        {
                        }
                        //if (att.IsNewSFSignal && att.RSIMode == IndicatorMode.Sell && att.MACDMode == IndicatorMode.Sell && att.AdxMode == IndicatorMode.Sell)
                        if (position.PositionSettings.Rule6 && att.STMode == IndicatorMode.Sell && att.RSIMode == IndicatorMode.Sell && att.MACDMode == IndicatorMode.Sell && att.AdxMode == IndicatorMode.Sell && Math.Abs(decimal.Subtract(att.PostiveDIn, att.NegativeDIn)) >= 7)// && Math.Abs(decimal.Subtract(att.MACD, att.SignalLine)) >= macdNoise)

                        {

                            att.Mode = IndicatorMode.Sell;
                        }

                        else if (position.PositionSettings.Rule7 && att.IsNewSFSignal && att.STMode == IndicatorMode.Sell && att.RSIMode == IndicatorMode.Sell && att.MACDMode == IndicatorMode.Sell && Math.Abs(decimal.Subtract(att.MACD, att.SignalLine)) >= macdNoise)
                        //if (att.RSIMode == IndicatorMode.Sell && att.MACDMode == IndicatorMode.Sell && att.AdxMode == IndicatorMode.Sell)// && Math.Abs(decimal.Subtract(att.PostiveDIn, att.NegativeDIn)) >= macdNoise)// && Math.Abs(decimal.Subtract(att.MACD, att.SignalLine)) >= macdNoise)
                        {

                            att.Mode = IndicatorMode.Sell;
                        }
                        else if (position.PositionSettings.Rule8 && att.STMode == IndicatorMode.Sell && att.RSIMode == IndicatorMode.Buy && att.MACDMode == IndicatorMode.Buy
                            && Math.Abs(decimal.Subtract(att.MACD, att.SignalLine)) > 3)
                        {
                            att.EarlyBird = true;
                            att.Mode = IndicatorMode.Buy;

                        }
                        else if (position.PositionSettings.Rule9 && fAttributes[i - 1].EarlyBird == true && att.STMode == IndicatorMode.Sell && att.RSIMode == IndicatorMode.Buy && att.MACDMode == IndicatorMode.Buy)
                        {
                            att.EarlyBird = true;
                            att.Mode = IndicatorMode.Buy;
                        }
                        else if (position.PositionSettings.Rule10 && att.STMode == IndicatorMode.Sell && att.RSIMode == IndicatorMode.Sell && att.MACDMode == IndicatorMode.Sell && Math.Abs(decimal.Subtract(att.MACD, att.SignalLine)) >= macdNoise)
                        //if (att.RSIMode == IndicatorMode.Sell && att.MACDMode == IndicatorMode.Sell && att.AdxMode == IndicatorMode.Sell)// && Math.Abs(decimal.Subtract(att.PostiveDIn, att.NegativeDIn)) >= macdNoise)// && Math.Abs(decimal.Subtract(att.MACD, att.SignalLine)) >= macdNoise)

                        {
                            att.Mode = IndicatorMode.Sell;
                        }
                        //new
                        else if (position.PositionSettings.Rule10 && att.STMode == IndicatorMode.Sell && att.RSIMode == IndicatorMode.Sell && att.AdxMode == IndicatorMode.Sell && att.MACDMode == IndicatorMode.Buy && Math.Abs(decimal.Subtract(fAttributes[i - 1].MACD, fAttributes[i - 1].SignalLine)) >= macdNoise)
                        //if (att.RSIMode == IndicatorMode.Sell && att.MACDMode == IndicatorMode.Sell && att.AdxMode == IndicatorMode.Sell)// && Math.Abs(decimal.Subtract(att.PostiveDIn, att.NegativeDIn)) >= macdNoise)// && Math.Abs(decimal.Subtract(att.MACD, att.SignalLine)) >= macdNoise)

                        {
                            att.Mode = IndicatorMode.Sell;
                        }
                        //
                        else
                            att.Mode = fAttributes[i - 1].Mode;// IndicatorMode.Buy;


                        if (false)
                        {
                            if (!position.PositionSettings.BuyRSITriggered && position.PositionSettings.BuyRSITrigger && position.PositionSettings.Rule5
                                && att.RSIEMA1 > att.RSIEMA2)
                            {
                                att.Mode = IndicatorMode.Buy;
                                position.PositionSettings.BuyRSITriggered = true;
                            }
                            if (!position.PositionSettings.BuyRSITrigger && position.PositionSettings.Rule5 && att.STMode == IndicatorMode.Sell && att.RSI < 15 && att.Mode == IndicatorMode.Sell)
                            {
                                position.PositionSettings.BuyRSITrigger = true;
                            }

                            if (!position.PositionSettings.BuyMACDTriggered && position.PositionSettings.BuyMACDTrigger && position.PositionSettings.Rule5 && att.MACD > att.SignalLine)
                            {
                                att.Mode = IndicatorMode.Buy;
                                position.PositionSettings.BuyMACDTriggered = true;
                            }
                            if (!position.PositionSettings.BuyMACDTrigger && position.PositionSettings.Rule5 && att.MACDMode == IndicatorMode.Sell && att.MACD < -70 && att.Mode == IndicatorMode.Sell)
                            {
                                position.PositionSettings.BuyMACDTrigger = true;
                            }

                            if (position.PositionSettings.BuyMACDTriggered)
                            {
                                att.Mode = IndicatorMode.Buy;
                            }
                            if (position.PositionSettings.BuyRSITriggered)
                            {
                                att.Mode = IndicatorMode.Buy;
                            }
                        }
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
        private static void UpdateRunSettings(Position position)
        {
            RunSettings runSettings = position.PositionSettings.runSettings;
            bestRunSettings.STMultiplier = runSettings.STMultiplier;
            bestRunSettings.STPeriod = runSettings.STPeriod;
            bestRunSettings.Points = runSettings.CurrentPoints;
            bestRunSettings.Trades = runSettings.Trades;
        }


    }
}
