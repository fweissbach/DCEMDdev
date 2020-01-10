using System;
using SmartQuant;
using SmartQuant.IB;

namespace OpenQuant
{
    public class LiveIB : Scenario
    {
        string symbols = "EURUSD,GBPUSD,USDJPY,EURGBP,USDCHF";
        long Lambda = 10;
        double Threshold = 0.67;
        double Cash = 10000;
        bool tradingDisabled = true; // set to true for trading sessions

        // closeMode = 0:  cross of 0 closes the position
        // closeMode = 1:  long:crossFromBelow(envU) and short:crossFromAbove(envL) closes trade
        // closeMode = 2:  crossFromAbove(envU) and crossFromBelow(envL) closes trade
        int CloseMode = 1;

        public LiveIB(Framework framework)
            : base(framework)
        {
        }

        public override void Run()
        {
            // Prepare running.
            Console.WriteLine("Prepare running in {0} mode...", StrategyManager.Mode);

            // Connect to Interactive Broker
            IProvider provider = ProviderManager.GetProvider("IB");
            if (provider.Status == ProviderStatus.Disconnected)
                provider.Connect(10);

            // setup the instruments:
            // A 1 tick barfactory drives the datastream to reduce the eventflow
            // Direction changes are detected and emitted by the DCBarFactoryItem
            InstrumentList instruments = new InstrumentList();
            foreach (string symbol in symbols.Split(','))
            {
                Instrument instrument = InstrumentManager.Instruments[symbol];

                BarFactory.Add(new DCBarFactoryItem(instrument, Lambda));
                BarFactory.Add(instrument, BarType.Tick, 1, BarInput.Middle);
                instruments.Add(instrument);
            }

            // settings for quotes
            this.SetupForQuoteStream();
            framework.CurrencyConverter = new MyCurrencyConverter(this, instruments, false);

            // filter to avoid level2 data
            EventManager.Filter = new Level2EventFilter(framework);


            // Setup a main Strategy
            strategy = new Strategy(framework, "DCEMDTrading");

  
            StatisticsManager.Statistics.Add(new TotalSlippageQ());
            StatisticsManager.Statistics.Add(new SlippageBps());
            StatisticsManager.Statistics.Add(new TotalSlippage());


            // Create the BuySide strategy and add the imf instrument
            strategy = new DCEMDStrategy(framework, "DCEMDStrategy");
            strategy.AddInstruments(instruments);
            strategy.SetParameter("Envelope", Threshold * Lambda);
            strategy.SetParameter("Lambda", Lambda);
            strategy.SetParameter("Cash", Cash);
            strategy.SetParameter("CloseMode", CloseMode);
            strategy.SetParameter("TradingDisabled", tradingDisabled);

            // Live stage
            Console.WriteLine("Run in Live mode.");
            StrategyManager.Mode = StrategyMode.Live;
            framework.CurrencyConverter = new MyCurrencyConverter(this, instruments, false);
            StartStrategy(StrategyMode.Live);

        }
    }

    public class MyEventFilter : EventFilter
	{
		public Level2EventFilter(Framework framework)
			: base(framework)
		{
		}

		public override Event Filter(Event e)
        {
            try
            {
                switch (e.TypeId)
                {
                    case DataObjectType.Level2:
                    case DataObjectType.Level2Snapshot:
                    case DataObjectType.Level2Update:
                        return null;                     
                }
                return e;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in MyEventsFilter. Error is: {0}", ex.Message);
                return null;
            }
        }
	}
}

