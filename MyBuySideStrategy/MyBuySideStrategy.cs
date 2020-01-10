using System;
using System.Drawing;

using SmartQuant;
using SmartQuant.Indicators;

namespace OpenQuant
{
    public class IMFStrat : InstrumentStrategy
    {
        private Group imfGroup, envUGroup, envLGroup, fillGroup;
        private TimeSeries imf;
        private double positionLimit;

        public static bool TradingDisabled = false;

        [Parameter]
        public double Envelope;

        [Parameter]
        public double Lambda;

 

        // closeMode = 0:  cross of 0 closes the position
        // closeMode = 1:  long:crossFromBelow(envU) and short:crossFromAbove(envL) closes trade
        // closeMode = 2:  crossFromAbove(envU) and crossFromBelow(envL) closes trade
        [Parameter]
        public int CloseMode = 2;


        public IMFStrat(Framework framework, string name)
            : base(framework, name)
        {
        }


        protected override void OnStrategyInit()
        {
            imf = new TimeSeries();
            // std = new SMD(imf, 10000);
          //  onlineStd = new RunningSTD(imf);

            AddGroups();
        }

        protected override void OnStrategyStart()
        {
         
        }

        protected override void OnTrade(Instrument instrument, Trade trade)
        {
            Log(trade.DateTime, trade.Price, imfGroup);

            imf.Add(trade.DateTime, trade.Price);

            //     double envU = onlineStd.Last * Threshold;
            //     double envL = -onlineStd.Last * Threshold;
            double envU = Envelope;
            double envL = -Envelope;

            Log(trade.DateTime, envU, envUGroup);
            Log(trade.DateTime, envL, envLGroup);


            int index = imf.Count - 1;

            if (TradingDisabled)
                return;

            // Trading logic
            Cross highCross = imf.Crosses(envU, index);
            Cross lowCross = imf.Crosses(envL, index);
            Cross closeoutCross;
            double newtradeQty = 0.0;
            double closeoutQty = 0.0;

            if (trade.Size>0.0)
                positionLimit = trade.Size;

            if (CloseMode == 0)
            {
                closeoutCross = imf.Crosses(0.0, index);
                if (HasLongPosition() && closeoutCross == Cross.Above)
                    closeoutQty = - Position.Amount;
                else if (HasShortPosition() && closeoutCross == Cross.Below)
                    closeoutQty = - Position.Amount;
            }
            else
            {
                if (HasLongPosition())
                {
                    closeoutCross = imf.Crosses(envU, index);

                    if (CloseMode == 1 && closeoutCross == Cross.Above)
                        closeoutQty = -Position.Amount;
                    else if (CloseMode == 2 && closeoutCross == Cross.Below)
                        closeoutQty = -Position.Amount;
                }
                else if (HasShortPosition())
                {
                    closeoutCross = imf.Crosses(envL, index);

                    if (CloseMode == 1 && closeoutCross == Cross.Below)
                        closeoutQty = -Position.Amount;
                    else if (CloseMode == 2 && closeoutCross == Cross.Above)
                        closeoutQty = -Position.Amount;
                }
            }

            if (highCross == Cross.Below)
                newtradeQty = - positionLimit;
            else if (lowCross == Cross.Above)
                newtradeQty = positionLimit;

            double tradeQty = newtradeQty + closeoutQty;
            if (tradeQty > positionLimit * 0.05)
                Buy(instrument, newtradeQty, "imf");
            else if (tradeQty < - positionLimit * 0.05)
                Sell(instrument, -newtradeQty, "imf");
        }

        protected override void OnFill(Fill fill)
        {
            Log(fill, fillGroup);
        }

        protected override void OnStrategyStop()
        {
            Console.WriteLine(Instrument.Symbol + " std:" + imf.GetStdDev() + " Threshold:" + Envelope);
        }

        private void AddGroups()
        {
            imfGroup = new Group("imf");
            imfGroup.Add("Pad", DataObjectType.String, 1);
            imfGroup.Add("SelectorKey", Instrument.Legs[0].Symbol);
            imfGroup.Add("Color", Color.Blue);
            imfGroup.Add("Width", 1);

            envUGroup = new Group("envU");
            envUGroup.Add("Pad", DataObjectType.String, 1);
            envUGroup.Add("SelectorKey", Instrument.Legs[0].Symbol);
            envUGroup.Add("Color", Color.Black);
            envUGroup.Add("Width", 1);


            envLGroup = new Group("envL");
            envLGroup.Add("Pad", DataObjectType.String, 1);
            envLGroup.Add("SelectorKey", Instrument.Legs[0].Symbol);
            envLGroup.Add("Color", Color.Black);
            envLGroup.Add("Width", 1);

            // Create fills group.
            fillGroup = new Group("IMF fills");
            fillGroup.Add("Pad", 1);
            fillGroup.Add("Format", "F1");
            fillGroup.Add("SelectorKey", Instrument.Legs[0].Symbol);
            fillGroup.Add("TextEnabled", false);

            // Add groups to manager.
            GroupManager.Add(imfGroup);
            GroupManager.Add(envUGroup);
            GroupManager.Add(envLGroup);
            GroupManager.Add(fillGroup);
        }
    }
}

