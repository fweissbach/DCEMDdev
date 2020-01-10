using System;
using SmartQuant;
using SmartQuant.IB;    

namespace OpenQuant
{
    public class PaperIB : Scenario
    {
        string symbols = "EURUSD,GBPUSD,USDJPY,EURGBP,USDCHF,AUDJPY,AUDUSD,EURJPY";
        long Lambda = 10;
        double Threshold = 0.67;
        double Cash = 100000;
        bool tradingDisabled = false; // set to true for trading sessions

        // closeMode = 0:  cross of 0 closes the position
        // closeMode = 1:  long:crossFromBelow(envU) and short:crossFromAbove(envL) closes trade
        int CloseMode = 1;

        public PaperIB(Framework framework)
            : base(framework)
        {
        }

        public override void Run()
        {
            StrategyManager.Mode = StrategyMode.Live;

            // Connect to Interactive Broker
            IProvider provider = ProviderManager. GetProvider("IB");
            if (provider.Status == ProviderStatus.Disconnected)
                provider.Connect(10);

            // Setup a main Strategy
            DCEMDStrategy dcemdstrategy = new DCEMDStrategy(framework, "DCEMD paper Trading");

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
            dcemdstrategy.AddInstruments(instruments);

            strategy = dcemdstrategy;

            strategy.DataProvider = provider as IDataProvider;
            strategy.ExecutionProvider = provider as IExecutionProvider;

            // what to do ona restart
            // •	None – do nothing
            // •	Load – load execution messages and restore portfolios and orders before strategy run
            // •	Save – save portfolio tree and execution messages during strategy run
            // •	Full – load and save
            StrategyManager.Persistence = StrategyPersistence.Save;

            framework.CurrencyConverter = new MyCurrencyConverter(this, instruments, false);
            
            // Set event filter.
			EventManager.Filter = new Level2EventFilter(framework);
            
            strategy.SetParameter("Envelope", Threshold * Lambda);
            strategy.SetParameter("Lambda", Lambda);
            strategy.SetParameter("Cash", Cash);
            strategy.SetParameter("CloseMode", CloseMode);
            strategy.SetParameter("TradingDisabled", tradingDisabled);

            // Live stage
            Console.WriteLine("Run in Live mode.");
            StartStrategy(StrategyMode.Live);

        }
    }
}









