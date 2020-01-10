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

        private bool PosOpened = false;


        [Parameter]
        public bool UseStopLoss = true;

        [Parameter]
        public bool InSession = true;

        [Parameter]
        public double Lambda = 10.0;

        [Parameter]
        public double PositionLimit;

        [Parameter]
        public int NrExtrema = 2;

        [Parameter]
        public int NrWarmup = 2;

        [Parameter]
        public double Cash = 100000;

        [Parameter]
        public double Envelope;

        [Parameter]
        public int CloseMode = 1;

        [Parameter]
        public TimeSpan SessionStart = new TimeSpan(21, 30, 0);

        [Parameter]
        public TimeSpan SessionEnd = new TimeSpan(20, 45, 0);


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
            // add reminder for EO Trading date
            // Start the Strategy during a session
            InSession = true;
            AddReminder(Clock.DateTime.Date.Add(SessionEnd));
        }

        protected override void OnReminder(DateTime dateTime, object data)
        {
            if (SessionStart > SessionEnd)
            // Session end and start on the same date, for example
            // end: 21:55
            // start: 22.30
            {
                if (InSession)
                // Close Position, Stop trading and set reminder to next dat trading start
                {
                    InSession = false;
                    AddReminder(Clock.DateTime.Date.Add(SessionStart));
                    Console.WriteLine(Instrument.ToString()+" Session start at " + Clock.DateTime.Date.Add(SessionStart).ToString());

                    Bars.Clear();

                    if (HasShortPosition())
                    {
                        Buy(Instrument, Position.Qty, "EOD close");
                        PosOpened = false;
                    }
                    else if (HasLongPosition())
                    {
                        Sell(Instrument, Position.Qty, "EOD close");
                        PosOpened = false;
                    }
                }
                else          
                // Start trading and set reminder to trading end
                {
                    InSession = true;
                    AddReminder(Clock.DateTime.Date.AddBusinessDays(1).Add(SessionEnd));
                    Console.WriteLine(Instrument.ToString() + " Session ends at " + Clock.DateTime.Date.AddBusinessDays(1).Add(SessionEnd).ToString());

                    if (ExecutionProvider.IsDisconnected && !ExecutionProvider.IsConnecting)
                        ExecutionProvider.Connect();
                }
            }
            else
            // Session end and start on the different date, for example
            // end: 21:55
            // start: 04.30
            {
                if (InSession)
                // Close Position, Stop trading and set reminder to next dat trading start
                {
                    InSession = false;
                    AddReminder(Clock.DateTime.Date.AddBusinessDays(1).Add(SessionStart));
                    Console.WriteLine(Instrument.ToString() + " Session start at " + Clock.DateTime.Date.AddBusinessDays(1).Add(SessionStart).ToString());

                    if (HasShortPosition())
                    {
                        Buy(Instrument, Position.Qty, "EOD close");
                        PosOpened = false;
                    }
                    else if (HasLongPosition())
                    {
                        Sell(Instrument, Position.Qty, "EOD close");
                        PosOpened = false;
                    }
                }
                else
                // Start trading and set reminder to trading end
                {
                    InSession = true;
                    AddReminder(Clock.DateTime.Date.Add(SessionEnd));
                    Console.WriteLine(Instrument.ToString() + " Session ends at " + Clock.DateTime.Date.Add(SessionEnd).ToString());

                    if (ExecutionProvider.IsDisconnected && !ExecutionProvider.IsConnecting)
                        ExecutionProvider.Connect();
                }
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

                // start predictions after the warmup.
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

            if (!InSession)
                return;

            // Compact trade logic
            // if there is a long/short postion then close it if the opposite envelope or 0 is crossed
            // if there is no position then open it if the envelope is crosses

            if (Bars.Count <= NrWarmup)
                return;

            Cross highCross = imf.Crosses(envU, index);
            Cross lowCross = imf.Crosses(envL, index);
            Cross zeroCross = imf.Crosses(0.0, index);

            // handling of Long position
            if (HasLongPosition() && PosOpened)
            {
                if (zeroCross == Cross.Above && CloseMode == 0)
                {
                    Sell(Instrument, Position.Qty, "closing@zero trade");
                    PosOpened = false;
                }
                else if (highCross == Cross.Above && CloseMode == 1)
                {
                    Sell(Instrument, Position.Qty, "closing@envU trade");
                    PosOpened = false;
                }
                else if (UseStopLoss && imfTick.Price<-Lambda)
                {
                    Sell(Instrument, Position.Qty, "Stoploss hit");
                    PosOpened = false;
                }
            }
            // handling of Short position
            else if (HasShortPosition() && PosOpened)
            {
                if (zeroCross == Cross.Below && CloseMode == 0)
                {
                    Buy(Instrument, Position.Qty, "closing@zero trade");
                    PosOpened = false;
                }
                else if (lowCross == Cross.Below && CloseMode == 1)
                {
                    Buy(Instrument, Position.Qty, "closing@envL trade");
                    PosOpened = false;
                }
                else if (UseStopLoss && imfTick.Price > Lambda)
                {
                    Buy(Instrument, Position.Qty, "Stoploss hit");
                    PosOpened = false;
                }
            }
            // handling of No position
            else
            {
                if (lowCross == Cross.Above)
                {
                    if (!PosOpened)
                    {
                        Buy(Instrument, PositionLimit, "opening trade");
                        PosOpened = true;
                    }
                    else
                        Console.WriteLine(Clock.DateTime.ToLongTimeString() + ":" + Instrument.Symbol + " opening buy ignored");
                }
                else if (highCross == Cross.Below)
                {
                    if (!PosOpened)
                    {
                        Sell(Instrument, PositionLimit, "opening trade");
                        PosOpened = true;
                    }
                    else
                        Console.WriteLine(Clock.DateTime.ToLongTimeString() + ":" + Instrument.Symbol + " opening sell ignored");
                }
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






