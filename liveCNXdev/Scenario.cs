using System;
using SmartQuant;
using SmartQuant.CX;

namespace OpenQuant
{
    public class liveCNXdev : Scenario
    {
		string symbols = "AUDJPY,AUDUSD,EURCHF,EURGBP,EURJPY,EURNOK,EURSEK,EURUSD,GBPUSD,NZDUSD,USDCAD,USDCHF,USDJPY";
        long Lambda = 10;
        double Threshold = 0.67;
        double Cash = 200000; // EUR 500k position size and deposit
        bool UseStopLoss = true;  // IMF based StopLoss
        double SLlevel = 20.0; // IMF base stoploss level

        // closeMode = 0:  cross of 0 closes the position
        // closeMode = 1:  long:crossFromBelow(envU) and short:crossFromAbove(envL) closes trade
        int CloseMode = 0;

        public liveCNXdev(Framework framework)
            : base(framework)
        {
        }

        public override void Run()
        {
            StrategyManager.Mode = StrategyMode.Live;

            // Connect to Interactive Broker
            IProvider provider = ProviderManager.GetProvider("Currenex");
            if (provider.Status == ProviderStatus.Disconnected)
                provider.Connect(10);

            // Setup a main Strategy
            CMXdev CNXdev = new CMXdev(framework, "DCEMD CMX development");

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
            CNXdev.AddInstruments(instruments);

            strategy = CNXdev;

            strategy.DataProvider = provider as IDataProvider;
            strategy.ExecutionProvider = provider as IExecutionProvider;

            AccountDataSnapshot accData = AccountDataManager.GetSnapshot(4, 4);
			Console.WriteLine(accData.ToString());


            CNXCommission cnxCommission = new CNXCommission();
            ExecutionSimulator.CommissionProvider = cnxCommission;

            // what to do ona restart
            // �	None � do nothing
            // �	Load � load execution messages and restore portfolios and orders before strategy run
            // �	Save � save portfolio tree and execution messages during strategy run
            // �	Full � load and save
            // StrategyManager.Persistence = StrategyPersistence.Save;

            framework.CurrencyConverter = new MyCurrencyConverter(this, instruments, true);

            // Set event filter.
            // EventManager.Filter = new IBForexFilter(framework);

            strategy.SetParameter("Envelope", Threshold * Lambda);
            strategy.SetParameter("Lambda", Lambda);
            strategy.SetParameter("Cash", Cash);
            strategy.SetParameter("CloseMode", CloseMode);
            strategy.SetParameter("UseStopLoss", UseStopLoss);
            strategy.SetParameter("SLlevel", SLlevel);
			strategy.SetParameter("ItmLimitBPS", 0.15);
			

            // Live stage
            Console.WriteLine("Run in Live mode.");
            StartStrategy(StrategyMode.Live);

        }
    }
}





