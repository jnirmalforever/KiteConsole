using KiteConnect;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;

namespace KiteConsole_v2
{

    public class Algo
    {
        private Position PullIndicatorData(Settings settings, Kite kite)
        {
            IterateOnIndicator indicator = new IterateOnIndicator();
            return indicator.IndicatorCheck(settings, kite);
        }

        public void Main(Kite kite, Settings settings)
        {
            Position position60Min = new Position();
            Position positionDay = new Position();
            Position position30Min = new Position();

            settings.ParentInstrument = "81153";
            position60Min.PositionSettings = Misc.DeepClone(settings);
            positionDay.PositionSettings = Misc.DeepClone(settings);
            position30Min.PositionSettings = Misc.DeepClone(settings);

            position30Min.PositionSettings.Interval = "30minute";
            position30Min.PositionSettings.FromDate = Program.GetIndianDateTime().AddDays(-180);
            position30Min.PositionSettings.ToDate = Program.GetIndianDateTime();

            position60Min.PositionSettings.Interval = "60minute";
            position60Min.PositionSettings.FromDate = Program.GetIndianDateTime().AddDays(-360);
            position60Min.PositionSettings.ToDate = Program.GetIndianDateTime();

            positionDay.PositionSettings.Interval = "day";
            positionDay.PositionSettings.FromDate = Program.GetIndianDateTime().AddDays(-540);
            positionDay.PositionSettings.ToDate = Program.GetIndianDateTime();

            //position30Min.PositionSettings.Interval = "30minute";
            //position30Min.PositionSettings.FromDate = Program.GetIndianDateTime().AddDays(-800);
            //position30Min.PositionSettings.ToDate = Program.GetIndianDateTime().AddDays(-650);

            //position60Min.PositionSettings.Interval = "60minute";
            //position60Min.PositionSettings.FromDate = Program.GetIndianDateTime().AddDays(-800);
            //position60Min.PositionSettings.ToDate = Program.GetIndianDateTime().AddDays(-500);


            //positionDay.PositionSettings.Interval = "day";
            //positionDay.PositionSettings.FromDate = Program.GetIndianDateTime().AddDays(-800);
            //positionDay.PositionSettings.ToDate = Program.GetIndianDateTime().AddDays(-500);


            IterateOnIndicator indicator = new IterateOnIndicator();
            position30Min = indicator.IndicatorCheck(position30Min.PositionSettings, kite);
            position60Min = indicator.IndicatorCheck(position60Min.PositionSettings, kite);
            positionDay = indicator.IndicatorCheck(positionDay.PositionSettings, kite);

            //AnalyzeTrade1(positionDay, position60Min);
            AnalyzeTrade1(positionDay, position60Min, position30Min);
        }

        private void AnalyzeTrade1(Position positionDay, Position position60Min, Position position30Min)
        {
            string connectionString = ConfigurationManager.AppSettings["KiteDB"];
            SqlConnection conn = new SqlConnection(connectionString);
            conn.Open();
            int trades = 1;
            decimal startPrice = 0, endPrice = 0;
            IndicatorMode startMode, endMode;
            foreach (var dayAtt in positionDay.PositionAttributes)
            {
                int i = positionDay.PositionAttributes.IndexOf(dayAtt);
                if (i > 14)
                {
                    if (dayAtt.TimeStamp > Program.GetIndianDateTime().AddDays(-10))
                    {

                    }
                    IEnumerable<Attributes> positionFiltered = position60Min.PositionAttributes
                        .Where(x => x.TimeStamp >= dayAtt.TimeStamp)
                        .Where(x => x.TimeStamp < dayAtt.TimeStamp.AddDays(1)).ToList();

                    if (positionFiltered.Count() > 0)
                    {

                    }

                    foreach (var subAtt60 in positionFiltered)
                    {
                        if (subAtt60.STMode != IndicatorMode.None)
                        {
                            if (dayAtt.STMode == subAtt60.STMode)
                            {
                                subAtt60.STModeDay60Min = dayAtt.STMode;
                            }
                            else
                            {
                                subAtt60.STModeDay60Min = IndicatorMode.SquareOff;
                            }
                        }
                    }
                }
            }


            foreach (var subAtt60 in position60Min.PositionAttributes)
            {
                if (subAtt60.Close == 2560.05M)
                {

                }
                if (subAtt60.STModeDay60Min != IndicatorMode.None) //&& subAtt60.STModeDay60Min != IndicatorMode.SquareOff)
                {

                    IEnumerable<Attributes> positionFiltered30 = position30Min.PositionAttributes
                        .Where(x => x.TimeStamp >= subAtt60.TimeStamp)
                        .Where(x => x.TimeStamp <= subAtt60.TimeStamp.AddMinutes(30)).ToList();


                    foreach (var subAtt30 in positionFiltered30)
                    {
                        int j = position30Min.PositionAttributes.IndexOf(subAtt30);

                        if (subAtt30.Close == 2516)
                        {

                        }
                        if (subAtt30.Close == 2560.05M)
                        {

                        }

                        if (subAtt30.STMode == IndicatorMode.Buy)
                        {

                        }

                        if (subAtt30.STMode != IndicatorMode.None)
                        {
                            if (subAtt30.Close == 2404M)
                            {

                            }
                            if (subAtt30.Close == 2538.65M)
                            {

                            }

                            if (subAtt60.STModeDay60Min == subAtt30.STMode)
                            {
                                subAtt30.STModeDay30Min = subAtt60.STModeDay60Min;
                            }
                            else
                            {
                                subAtt30.STModeDay30Min = IndicatorMode.SquareOff;
                            }

                            if (subAtt30.STModeDay30Min != position30Min.PositionAttributes[j - 1].STModeDay30Min)
                            {
                            }

                            if (subAtt30.STModeDay30Min != position30Min.PositionAttributes[j - 1].STModeDay30Min)
                            {
                                Program.LogBackTest_Algo(position30Min, subAtt30.Close, subAtt30.Close, subAtt30, conn, trades, 0, subAtt30.TimeStamp, subAtt60.TimeStamp, subAtt30.STModeDay30Min);
                                trades++;
                            }
                        }
                    }
                }

            }

        }

        private void AnalyzeTrade(Position positionDay, Position position60Min, Position position30Min)
        {
            string connectionString = ConfigurationManager.AppSettings["KiteDB"];
            SqlConnection conn = new SqlConnection(connectionString);
            conn.Open();
            int trades = 1;
            foreach (var dayAtt in positionDay.PositionAttributes)
            {
                int i = positionDay.PositionAttributes.IndexOf(dayAtt);
                if (i > 14)
                {
                    if (dayAtt.TimeStamp > Program.GetIndianDateTime().AddDays(-10))
                    {

                    }
                    IEnumerable<Attributes> positionFiltered = position60Min.PositionAttributes.Where(x => x.TimeStamp >= dayAtt.TimeStamp.AddDays(-2));
                    foreach (var subAtt60 in positionFiltered)
                    {
                        if (subAtt60.TimeStamp >= dayAtt.TimeStamp && subAtt60.TimeStamp <= dayAtt.TimeStamp.AddDays(1)
                            && subAtt60.STMode != IndicatorMode.None)
                        {
                            if (dayAtt.STMode == subAtt60.STMode)
                            {
                                subAtt60.STModeDay60Min = dayAtt.STMode;
                            }
                            else
                            {
                                subAtt60.STModeDay60Min = IndicatorMode.SquareOff;
                            }
                        }
                    }
                }
            }

            IEnumerable<Attributes> positionFiltered60 = position60Min.PositionAttributes.Where(
                      x => (x.TimeStamp >= position30Min.PositionAttributes[0].TimeStamp.AddDays(-1)));

            foreach (var subAtt60 in positionFiltered60)
            {
                if (subAtt60.STModeDay60Min != IndicatorMode.None && subAtt60.STModeDay60Min != IndicatorMode.SquareOff)
                {
                    IEnumerable<Attributes> positionFiltered = position30Min.PositionAttributes.Where(
                        x => (x.TimeStamp >= subAtt60.TimeStamp.AddMinutes(-90)));
                    foreach (var subAtt30 in position30Min.PositionAttributes)
                    {
                        int j = position30Min.PositionAttributes.IndexOf(subAtt30);

                        //if (subAtt.TimeStamp <= dayAtt.TimeStamp && subAtt.TimeStamp >= positionDay.PositionAttributes[i - 1].TimeStamp)
                        //{

                        //}

                        if (subAtt30.Close == 2516)
                        {

                        }

                        if (subAtt30.STMode == IndicatorMode.Buy)
                        {

                        }

                        if (subAtt30.TimeStamp >= subAtt60.TimeStamp && subAtt30.TimeStamp <= subAtt60.TimeStamp.AddMinutes(30)
                         && subAtt30.STMode != IndicatorMode.None)
                        {
                            if (subAtt30.Close == 2404M)
                            {

                            }
                            if (subAtt30.Close == 2538.65M)
                            {

                            }

                            if (subAtt60.STModeDay60Min == subAtt30.STMode)
                            {
                                subAtt30.STModeDay30Min = subAtt60.STModeDay60Min;
                            }
                            else
                            {
                                subAtt30.STModeDay30Min = IndicatorMode.SquareOff;
                            }

                            if (subAtt30.STModeDay30Min != position30Min.PositionAttributes[j - 1].STModeDay30Min)
                            {

                            }

                            if (subAtt30.STModeDay30Min != position30Min.PositionAttributes[j - 1].STModeDay30Min)
                            {
                                Program.LogBackTest_Algo(position30Min, subAtt30.Close, subAtt30.Close, subAtt30, conn, trades, 0, subAtt30.TimeStamp, subAtt60.TimeStamp, subAtt30.STModeDay30Min);
                                trades++;
                            }
                        }
                    }
                }

            }

        }

        private void AnalyzeTrade(Position positionDay, Position position30Min)
        {
            string connectionString = ConfigurationManager.AppSettings["KiteDB"];
            SqlConnection conn = new SqlConnection(connectionString);
            conn.Open();
            int trades = 1;
            foreach (var dayAtt in positionDay.PositionAttributes)
            {
                int i = positionDay.PositionAttributes.IndexOf(dayAtt);
                if (i > 14)
                {
                    if (dayAtt.TimeStamp > Program.GetIndianDateTime().AddDays(-10))
                    {

                    }
                    //IEnumerable<Attributes> positionFiltered = position30Min.PositionAttributes.Where(x => x.TimeStamp >= dayAtt.TimeStamp.AddDays(-2));
                    foreach (var subAtt in position30Min.PositionAttributes)
                    {
                        int j = position30Min.PositionAttributes.IndexOf(subAtt);

                        //if (subAtt.TimeStamp <= dayAtt.TimeStamp && subAtt.TimeStamp >= positionDay.PositionAttributes[i - 1].TimeStamp)
                        //{

                        //}

                        if (subAtt.TimeStamp > Program.GetIndianDateTime().AddDays(-10))
                        {

                        }
                        if (subAtt.Close == 2516M)
                        {

                        }

                        if (subAtt.TimeStamp >= dayAtt.TimeStamp && subAtt.TimeStamp <= dayAtt.TimeStamp.AddDays(1) && subAtt.STMode != IndicatorMode.None)
                        {
                            if (subAtt.Close == 2404M)
                            {

                            }
                            if (dayAtt.STMode == subAtt.STMode)
                            {
                                subAtt.STModeDay30Min = dayAtt.STMode;
                            }
                            else
                            {
                                subAtt.STModeDay30Min = IndicatorMode.SquareOff;
                            }

                            if (subAtt.STModeDay30Min != position30Min.PositionAttributes[j - 1].STModeDay30Min)
                            {

                            }


                            if (subAtt.STModeDay30Min != position30Min.PositionAttributes[j - 1].STModeDay30Min)
                            {
                                Program.LogBackTest_Algo(position30Min, subAtt.Close, subAtt.Close, subAtt, conn, trades, 0, subAtt.TimeStamp, subAtt.TimeStamp, subAtt.STModeDay30Min);
                                trades++;
                            }
                        }
                    }
                }

            }
        }


        private void AnalyzeTrade1(Position positionDay, Position position60Min)
        {
            string connectionString = ConfigurationManager.AppSettings["KiteDB"];
            SqlConnection conn = new SqlConnection(connectionString);
            conn.Open();
            int trades = 1;
            decimal startPrice = 0, endPrice = 0;
            IndicatorMode startMode, endMode;
            foreach (var dayAtt in positionDay.PositionAttributes)
            {
                int i = positionDay.PositionAttributes.IndexOf(dayAtt);
                if (i > 14)
                {
                    if (dayAtt.TimeStamp > Program.GetIndianDateTime().AddDays(-10))
                    {

                    }
                    //IEnumerable<Attributes> positionFiltered = position30Min.PositionAttributes.Where(x => x.TimeStamp >= dayAtt.TimeStamp.AddDays(-2));

                    IEnumerable<Attributes> positionFiltered = position60Min.PositionAttributes
                        .Where(x => x.TimeStamp >= dayAtt.TimeStamp)
                        .Where(x => x.TimeStamp < dayAtt.TimeStamp.AddDays(1)).ToList();

                    if (positionFiltered.Count() > 0)
                    {

                    }
                    foreach (var subAtt in positionFiltered)
                    {
                        int j = position60Min.PositionAttributes.IndexOf(subAtt);

                        //if (subAtt.TimeStamp <= dayAtt.TimeStamp && subAtt.TimeStamp >= positionDay.PositionAttributes[i - 1].TimeStamp)
                        //{

                        //}

                        if (subAtt.TimeStamp > Program.GetIndianDateTime().AddDays(-10))
                        {

                        }
                        if (subAtt.Close == 2516M)
                        {

                        }

                        if (subAtt.STMode != IndicatorMode.None)
                        {
                            if (subAtt.Close == 2404M)
                            {

                            }

                            if (dayAtt.STMode == subAtt.STMode)
                            {
                                subAtt.STModeDay60Min = dayAtt.STMode;
                            }
                            else
                            {
                                subAtt.STModeDay60Min = IndicatorMode.SquareOff;
                            }

                            if (subAtt.STModeDay60Min != position60Min.PositionAttributes[j - 1].STModeDay60Min)
                            {
                                if (subAtt.STModeDay60Min == IndicatorMode.Sell)
                                {
                                    startPrice = subAtt.Close;
                                }
                                else if (subAtt.STModeDay60Min == IndicatorMode.Buy)
                                {
                                    endPrice = subAtt.Close;
                                }
                                else if (subAtt.STModeDay60Min == IndicatorMode.SquareOff)
                                {
                                    if (position60Min.PositionAttributes[j - 1].STModeDay60Min == IndicatorMode.Sell)
                                        endPrice = subAtt.Close;
                                    else if (position60Min.PositionAttributes[j - 1].STModeDay60Min == IndicatorMode.Buy)
                                        startPrice = subAtt.Close;

                                }
                            }


                            if (subAtt.STModeDay60Min != position60Min.PositionAttributes[j - 1].STModeDay60Min)
                            {
                                if (position60Min.PositionAttributes[j - 1].STModeDay60Min == IndicatorMode.SquareOff)
                                {

                                    Program.LogBackTest_Algo(position60Min, subAtt.Close, subAtt.Close, subAtt, conn, trades, 0, subAtt.TimeStamp, subAtt.TimeStamp, subAtt.STModeDay60Min);

                                }
                                else
                                {
                                    if (position60Min.PositionAttributes[j - 1].STModeDay60Min == IndicatorMode.Buy)

                                    {
                                        Program.LogBackTest_Algo(position60Min, 2, subAtt.Close, subAtt, conn, trades, startPrice - endPrice, subAtt.TimeStamp, subAtt.TimeStamp, subAtt.STModeDay60Min);

                                    }
                                    else
                                        Program.LogBackTest_Algo(position60Min, subAtt.Close, subAtt.Close, subAtt, conn, trades, startPrice - endPrice, subAtt.TimeStamp, subAtt.TimeStamp, subAtt.STModeDay60Min);
                                }
                                trades++;
                            }
                        }
                    }
                }

            }
        }

        private void AnalyzeTrade2(Position positionDay, Position position60Min)
        {
            string connectionString = ConfigurationManager.AppSettings["KiteDB"];
            SqlConnection conn = new SqlConnection(connectionString);
            conn.Open();
            int trades = 1;
            foreach (var dayAtt in positionDay.PositionAttributes)
            {
                int i = positionDay.PositionAttributes.IndexOf(dayAtt);
                if (i > 14)
                {
                    if (dayAtt.TimeStamp > Program.GetIndianDateTime().AddDays(-10))
                    {

                    }

                    IEnumerable<Attributes> positionFiltered = position60Min.PositionAttributes
                        .Where(x => x.TimeStamp >= dayAtt.TimeStamp)
                        .Where(x => x.TimeStamp < dayAtt.TimeStamp.AddDays(1)).ToList();

                    if (positionFiltered.Count() > 0)
                    {

                    }
                    foreach (var subAtt in positionFiltered)
                    {
                        int j = position60Min.PositionAttributes.IndexOf(subAtt);

                        if (subAtt.TimeStamp > Program.GetIndianDateTime().AddDays(-10))
                        {

                        }
                        if (subAtt.Close == 2516M)
                        {

                        }

                        if (subAtt.STMode != IndicatorMode.None)
                        {
                            if (subAtt.Close == 2404M)
                            {

                            }

                            if (dayAtt.STMode == subAtt.STMode)
                            {
                                if (subAtt.PostiveDIn < subAtt.NegativeDIn && dayAtt.STMode == IndicatorMode.Buy)
                                {
                                    subAtt.STModeDay60Min = IndicatorMode.SquareOff;
                                }
                                if (subAtt.PostiveDIn > subAtt.NegativeDIn && dayAtt.STMode == IndicatorMode.Sell)
                                {
                                    subAtt.STModeDay60Min = IndicatorMode.SquareOff;
                                }
                                else
                                {
                                    subAtt.STModeDay60Min = dayAtt.STMode;
                                }
                            }
                            else
                            {
                                subAtt.STModeDay60Min = IndicatorMode.SquareOff;
                            }

                            if (subAtt.STModeDay60Min != position60Min.PositionAttributes[j - 1].STModeDay60Min)
                            {

                            }


                            if (subAtt.STModeDay60Min != position60Min.PositionAttributes[j - 1].STModeDay60Min)
                            {
                                Program.LogBackTest_Algo(position60Min, subAtt.Close, subAtt.Close, subAtt, conn, trades, 0, subAtt.TimeStamp, subAtt.TimeStamp, subAtt.STModeDay60Min);
                                trades++;
                            }
                        }
                    }
                }

            }
        }

    }
}
