using KiteConnect;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace KiteConsole_v2
{
    public class IterateOnIndicator
    {
        public Position IndicatorCheck(Settings positionSettings, Kite kite)
        {
            List<Attributes> attributes = null;
            List<Historical> historical;
            IIndicator reqIndicator = null;

            if (true)
            {
                Thread.Sleep(1000);

                //positionSettings.ParentInstrument = "884737";
                //positionSettings.Interval = "30minute";
                historical = Program.GetHistoricalData(kite, positionSettings.ParentInstrument, positionSettings.FromDate, positionSettings.ToDate, positionSettings.Interval, false);
                //historical = Program.GetHistoricalData(kite, positionSettings.ParentInstrument, GetIndianDateTime().AddDays(-360), GetIndianDateTime().AddDays(-180), positionSettings.Interval, false);
                //historical = Program.GetHistoricalData(kite, positionSettings.ParentInstrument, GetIndianDateTime().AddDays(-540), GetIndianDateTime().AddDays(-360), positionSettings.Interval, false);
                ///historical = Program.GetHistoricalData(kite, positionSettings.ParentInstrument, GetIndianDateTime().AddDays(-860), GetIndianDateTime().AddDays(-700), positionSettings.Interval, false);

                if (historical.Count > 0)
                {
                    attributes = historical.OrderBy(x => x.TimeStamp).Select(x => new Attributes
                    {
                        Close = x.Close,
                        TimeStamp = x.TimeStamp,
                        High = x.High,
                        Low = x.Low,
                        Open = x.Open,
                        Volume = x.Volume
                    }).OrderBy(x => x.TimeStamp).ToList();
                }
                else { return null; }
            }
            Position position = new Position()
            {
                PositionAttributes = attributes,
                PositionSettings = positionSettings
            };

            if (position.PositionAttributes != null && position.PositionAttributes.Count > 0)
            {
                if (false)
                {
                    reqIndicator = new Cloud();
                    position = reqIndicator.StartLooking(position.PositionSettings, kite, position.PositionAttributes);
                }

                if (false)
                {
                    reqIndicator = new MFI();
                    position = reqIndicator.StartLooking(position.PositionSettings, kite, position.PositionAttributes);
                }

                if (false)
                {
                    reqIndicator = new HA();
                    position = reqIndicator.StartLooking(position.PositionSettings, kite, position.PositionAttributes);
                }

                if (!positionSettings.IsAdxEnabled)
                {
                    reqIndicator = new ADX();
                    position = reqIndicator.StartLooking(position.PositionSettings, kite, position.PositionAttributes);
                }

                if (positionSettings.IsMACDEnabled)
                {
                    reqIndicator = new MACD();
                    position = reqIndicator.StartLooking(position.PositionSettings, kite, position.PositionAttributes);
                }

                if (positionSettings.IsSTEnabled)
                {
                    reqIndicator = new SuperTrend();
                    //positionSettings.IsTestRun = true;
                    position.PositionSettings.runSettings.STMultiplier = 3M;
                    position.PositionSettings.runSettings.STBasic = 3M;
                    //position.PositionSettings.runSettings.STMultiplier = 2.5M;
                    //position.PositionSettings.runSettings.STBasic = 2.5M;
                    position.PositionSettings.runSettings.STPeriod = 14;
                    position = reqIndicator.StartLooking(position.PositionSettings, kite, position.PositionAttributes);

                    position.PositionSettings.runSettings.STMultiplier = 6M;
                    position.PositionSettings.runSettings.STBasic = 6M;
                    position.PositionSettings.runSettings.STIteration = 3;
                    position = reqIndicator.StartLooking(position.PositionSettings, kite, position.PositionAttributes);
                }

                if (positionSettings.IsRSIEnabled)
                {
                    reqIndicator = new RSIv1();
                    position = reqIndicator.StartLooking(position.PositionSettings, kite, position.PositionAttributes);
                }
                return position;
            }
            return null;
        }
    }
}