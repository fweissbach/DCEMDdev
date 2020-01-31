using System;
using System.Drawing;

using SmartQuant;

namespace OpenQuant
{
    public class CMXdev : InstrumentStrategy
    {
        //private TimeSeries maxs, mins, ovs, ovsDur, tm, tmDur;
        //private TimeSeries maxsPred, minsPred;
        //private TimeSeries imf;
        private Group bidGroup, askGroup, tickGroup;
        //private Group imfGroup, imfFillGroup, envUGroup, envLGroup, fillGroup;
        //private double lastQuotePrice;
        //private double lastImfPrice;
        //private IMFPred imfPred;
        //private string orderFile, orderSession;

        // To avoid duplicate trades whilean order has not been confirmed yet
   //     private bool Locked;

        [Parameter]
        public byte BaseCcy = CurrencyId.EUR;

        [Parameter]
        public bool UseStopLoss = false;

        [Parameter]
        public bool InSession = true;

        [Parameter]
        public double Lambda = 10.0;

        [Parameter]
        public double SLlevel = 105.0;

        [Parameter]
        public double PositionLimit;

        [Parameter]
        public int NrExtrema = 2;

        [Parameter]
        public int NrWarmup = 4;

        [Parameter]
        public double Cash = 500000;

        [Parameter]
        public double Envelope;

        [Parameter]
        public int CloseMode = 1;

        [Parameter]
        public TimeSpan SessionStart = new TimeSpan(21, 30, 0);

        [Parameter]
        public TimeSpan SessionEnd = new TimeSpan(20, 45, 0);


        public CMXdev(Framework framework, string name)
            : base(framework, name)
        {
        }

        protected override void OnStrategyInit()
        {
            AddGroups();
        }

        protected override void OnStrategyStart()
        {
        }

        protected override void OnBar(Instrument instrument, Bar bar)
        {
            //Console.WriteLine(bar.ToString());
            Log(bar.Open, tickGroup);
			Console.WriteLine(instrument.Bid.DateTime.ToLongTimeString() +
				":"+instrument.Bid.Price.ToString()+
				"-----"+bar.OpenDateTime.ToLongTimeString()+
				":"+bar.Open.ToString()+
				"-----"+instrument.Bid.DateTime.ToLongTimeString()+
				":"+instrument.Ask.Price.ToString());	
        }

        protected override void OnAsk(Instrument instrument, Ask ask)
        {
            Log(ask.Price, askGroup);
        }

        protected override void OnBid(Instrument instrument, Bid bid)
        {
            Log(bid.Price, bidGroup);
        }

        private void AddGroups()
        {
            // Create bars group.

            // Create tickbar chart, show as red line
            tickGroup = new Group("Bid");
            tickGroup.Add("Pad", DataObjectType.String, 0);
            tickGroup.Add("SelectorKey", Instrument.Symbol);
            tickGroup.Add("Color", Color.LightGreen);
            tickGroup.Add("Width", 2);
            tickGroup.Add("Format", Instrument.PriceFormat);

            bidGroup = new Group("Bid");
            bidGroup.Add("Pad", DataObjectType.String, 0);
            bidGroup.Add("SelectorKey", Instrument.Symbol);
            bidGroup.Add("Color", Color.Red);
            bidGroup.Add("Width", 1);
            bidGroup.Add("Format", Instrument.PriceFormat);

            askGroup = new Group("Ask");
            askGroup.Add("Pad", DataObjectType.String, 0);
            askGroup.Add("SelectorKey", Instrument.Symbol);
            askGroup.Add("Color", Color.Blue);
            askGroup.Add("Width", 1);
            askGroup.Add("Format", Instrument.PriceFormat);


            // Add groups to manager.
            GroupManager.Add(askGroup);
            GroupManager.Add(bidGroup);
            GroupManager.Add(tickGroup);

        }
    }
}




