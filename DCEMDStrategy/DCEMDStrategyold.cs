using System;
using System.Drawing;

using SmartQuant;

namespace OpenQuant
{
    public class DCEMDStrategy : InstrumentStrategy
    {
        private TimeSeries maxs, mins, ovs, ovsDur, tm, tmDur;
        private TimeSeries maxsPred, minsPred;
        private TimeSeries imf;
        private Group quoteGroup, dcBarsGroup, equityGroup;
        private Group imfGroup, imfFillGroup, envUGroup, envLGroup, fillGroup;
        private double lastQuotePrice;
        private double lastImfPrice;
        private IMFPred imfPred;

        [Parameter]
        public bool TradingDisabled = false;

        [Parameter]
        public double Lambda = 5.0;

        [Parameter]
        public double PositionLimit;

        [Parameter]
        public int NrExtrema = 2;

        [Parameter]
        public int NrWarmup = 2;

        [Parameter]
        public double Cash = 20000;

        [Parameter]
        public double Envelope;

        [Parameter]
        public int CloseMode = 2;

        [Parameter]
        public TimeSpan SessionStart = new TimeSpan(0, 0, 0);

        [Parameter]
        public TimeSpan SessionEnd = new TimeSpan(22, 0, 0);


        public DCEMDStrategy(Framework framework, string name)
            : base(framework, name)
        {
        }

        protected override void OnStrategyInit()
        {
            maxs = new TimeSeries(); mins = new TimeSeries();
            ovs = new TimeSeries(); ovsDur = new TimeSeries();
            tm = new TimeSeries(); tmDur = new TimeSeries();
            maxsPred = new TimeSeries(); minsPred = new TimeSeries();
            lastQuotePrice = 0.0;
            imf = new TimeSeries();
            imfPred = new IMFPred(NrExtrema, Lambda / 10000.0);
            Portfolio.Account.Deposit(Cash, CurrencyId.USD, "inital deposit");
            AddGroups();
        }

        protected override void OnStrategyStart()
        {
            if (TradingDisabled)
            {
                // Start trading the next day if Time of day has passed the SessionStart
                if (Clock.DateTime.TimeOfDay < SessionStart)
                    AddReminder(Clock.DateTime.Date.Add(SessionStart));
                else
                    AddReminder(Clock.DateTime.Date.Add(SessionStart.Add(new TimeSpan(1, 0, 0, 0))));
            }
        }

        protected override void OnReminder(DateTime dateTime, object data)
        {
            if (TradingDisabled)
            // Start trading and set reminder to trading end
            {
                TradingDisabled = false;
                AddReminder(Clock.DateTime.Date.Add(SessionEnd));
            }
            else
            // Close Position, Stop trading and set reminder to next dat trading start
            {
                if (HasShortPosition())
                    Buy(Instrument, Position.Qty, "EOD close");
                else if (HasLongPosition())
                    Sell(Instrument,Position.Qty, "EOD close");

                TradingDisabled = true;
                AddReminder(Clock.DateTime.Date.Add(SessionStart.Add(new TimeSpan(1,0,0,0))));
            }
        }


        protected override void OnBar(Instrument instrument, Bar bar)
        {

            if (bar.Type == BarType.Tick)
            {
                // log the 1-TickBar if not in simulation mode
                if (DataProvider != ProviderManager.DataSimulator)
                    Log(bar.CloseDateTime, bar.Close, quoteGroup);

                // escape if the 1-TickBar price is unchanged
                if (lastQuotePrice == bar.Close) return;

                lastQuotePrice = bar.Close;

                if (Bars.Count >= NrWarmup)
                {
                    Tick tick = new Tick(bar.CloseDateTime, 0, instrument.Id, bar.Close, 0);
                    lastImfPrice = imfPred.PredictDC(tick);

                    Tick imfTick = new Tick(tick.DateTime, 0, instrument.Id, lastImfPrice, 0);
                    OnIMFTick(imfTick);
                }
                else
                {
                    // one time set of position limit
                    PositionLimit = Math.Round(framework.CurrencyConverter.Convert(Cash, CurrencyId.USD, instrument.CCY1)/100.0,0)*100.0;
                }
                return;
            }

            if (bar.Type == BarType.Range)
            {
                Portfolio.Performance.Update();
                Log(bar, dcBarsGroup);
                Bars.Add(bar);

                imfPred.OnDCBars(Bars);
                Log(bar.DateTime, Portfolio.Value, equityGroup);
             
            }

        }


        private void OnIMFTick(Tick imfTick)
        {
            Log(imfTick.DateTime, imfTick.Price, imfGroup);

            imf.Add(imfTick.DateTime, imfTick.Price);

            double envU = Envelope;
            double envL = -Envelope;

            Log(imfTick.DateTime, envU, envUGroup);
            Log(imfTick.DateTime, envL, envLGroup);


            int index = imf.Count - 1;

            if (TradingDisabled)
                return;

            if (CloseMode == 2)
            {
                Cross highCross = imf.Crosses(envU, index);
                Cross lowCross = imf.Crosses(envL, index);
                if (highCross == Cross.Below && !HasShortPosition())
                    Sell(Instrument, PositionLimit + Position.Qty);
                else if (lowCross == Cross.Above && !HasLongPosition())
                    Buy(Instrument, PositionLimit + Position.Qty);
            }
            else
            {
                if (HasPosition())
                {
                    if (CloseMode == 0)
                    {
                        Cross closeoutCross = imf.Crosses(0.0, index);
                        if (closeoutCross == Cross.Above && HasLongPosition())
                            Sell(Instrument, Position.Qty);
                        else if (closeoutCross == Cross.Below && HasShortPosition())
                            Buy(Instrument, Position.Qty);
                    }
                    else if (CloseMode == 1)
                    {
                        if (HasLongPosition())
                        {
                            Cross closeoutCross = imf.Crosses(envU, index);
                            if (closeoutCross == Cross.Above)
                                Sell(Instrument, Position.Qty);
                        }
                        else if (HasShortPosition())
                        {
                            Cross closeoutCross = imf.Crosses(envL, index);
                            if (closeoutCross == Cross.Above)
                                Buy(Instrument, Position.Qty);
                        }
                    }
                    return; // return here, so no new position while HasPosition() is true
                }

                // New Position section
                Cross highCross = imf.Crosses(envU, index);
                Cross lowCross = imf.Crosses(envL, index);
                if (highCross == Cross.Below)
                    Sell(Instrument, PositionLimit);
                else if (lowCross == Cross.Above)
                    Buy(Instrument, PositionLimit);

            }
        }


        protected override void OnFill(Fill fill)
        {
            Log(fill, fillGroup);
            Fill imfFill = new Fill(fill.DateTime, new Order(), Instrument, Instrument.CurrencyId,  fill.Side, fill.Qty, lastImfPrice);
            Log(imfFill, imfFillGroup);
        }



        private void AddGroups()
        {
            // Create bars group.
            dcBarsGroup = new Group("DC-Bars");
            dcBarsGroup.Add("Format", Instrument.PriceFormat);
            dcBarsGroup.Add("Pad", DataObjectType.String, 0);
            dcBarsGroup.Add("SelectorKey", Instrument.Symbol);
            dcBarsGroup.Add("ChartStyle", "Bar");

            // Create tickbar chart, show as red line
            quoteGroup = new Group("Quote");
            quoteGroup.Add("Pad", DataObjectType.String, 0);
            quoteGroup.Add("SelectorKey", Instrument.Symbol);
            quoteGroup.Add("Color", Color.Red);
            quoteGroup.Add("Width", 1);
            quoteGroup.Add("Format", Instrument.PriceFormat);

            equityGroup = new Group("equity");
            equityGroup.Add("Pad", DataObjectType.String, 2);
            equityGroup.Add("SelectorKey", Instrument.Symbol);
            equityGroup.Add("Format", "F0");

            imfGroup = new Group("imf");
            imfGroup.Add("Pad", DataObjectType.String, 1);
            imfGroup.Add("SelectorKey", Instrument.Symbol);
            imfGroup.Add("Color", Color.Blue);
            imfGroup.Add("Width", 1);

            envUGroup = new Group("envU");
            envUGroup.Add("Pad", DataObjectType.String, 1);
            envUGroup.Add("SelectorKey", Instrument.Symbol);
            envUGroup.Add("Color", Color.Black);
            envUGroup.Add("Width", 1);


            envLGroup = new Group("envL");
            envLGroup.Add("Pad", DataObjectType.String, 1);
            envLGroup.Add("SelectorKey", Instrument.Symbol);
            envLGroup.Add("Color", Color.Black);
            envLGroup.Add("Width", 1);

            // Create fills group.
            fillGroup = new Group("Fills");
            fillGroup.Add("Pad", 0);
            fillGroup.Add("Format", Instrument.PriceFormat);
            fillGroup.Add("SelectorKey", Instrument.Symbol);
            fillGroup.Add("TextEnabled", false);

            imfFillGroup = new Group("Fills");
            imfFillGroup.Add("Pad", 1);
            imfFillGroup.Add("Format", Instrument.PriceFormat);
            imfFillGroup.Add("SelectorKey", Instrument.Symbol);
            imfFillGroup.Add("TextEnabled", false);


            // Add groups to manager.
            GroupManager.Add(dcBarsGroup);
            GroupManager.Add(quoteGroup);
            GroupManager.Add(equityGroup);
            GroupManager.Add(imfGroup);
            GroupManager.Add(envUGroup);
            GroupManager.Add(envLGroup);
            GroupManager.Add(fillGroup);
            GroupManager.Add(imfFillGroup);

        }


    }
}


