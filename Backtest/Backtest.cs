using System;
using SmartQuant;

namespace OpenQuant
{
    public class Backtest : Scenario
    {

		string symbols = "GBPUSD";
        DateTime startDateTime = new DateTime(2018, 1, 1, 0, 0, 0);
        DateTime endDateTime = new DateTime(2019, 1, 1, 0, 0, 0);
        long Lambda = 10;
        double Threshold = 0.75;


        public Backtest(Framework framework)
            : base(framework)
        {
        }

        public override void Run()
        {
            // setup the instruments:
            // for every symbols an synthetic IMFinstrument is created, the real instrument is attached as leg.
            // A 1 tick barfactory drives the datastream to reduce the eventflow
            // Direction changes are detected and emitted by the DCBarFactoryItem
            InstrumentList imfInstruments = new InstrumentList();
            InstrumentList instruments = new InstrumentList();
            foreach (string symbol in symbols.Split(','))
            {
                Instrument instrument = InstrumentManager.Instruments[symbol];
                Instrument imfInstrument = InstrumentManager.Get("imf1" + symbol);

                if (imfInstrument != null)
                    InstrumentManager.Delete(imfInstrument);

                imfInstrument = new Instrument(InstrumentType.Synthetic, "imf1" + symbol);
                imfInstrument.Legs.Add(new Leg(instrument));
                InstrumentManager.Add(imfInstrument);

                imfInstruments.Add(imfInstrument);
                BarFactory.Add(new DCBarFactoryItem(instrument, Lambda));
                BarFactory.Add(instrument, BarType.Tick, 1, BarInput.Middle);
                instruments.Add(instrument);
            }

            // settings for quotes
            this.SetupForQuoteStream();
            framework.CurrencyConverter = new MyCurrencyConverter(this, instruments, false);

            // FillOnBar will simulate mid market trading
            // FillOnQuote generates slippage
            ExecutionSimulator.FillOnBar = true;
            ExecutionSimulator.FillOnQuote = false;

            DataSimulator.DateTime1 = startDateTime;
            DataSimulator.DateTime2 = endDateTime;

            StatisticsManager.Statistics.Add(new TotalSlippageQ());
            StatisticsManager.Statistics.Add(new SlippageBps());
            StatisticsManager.Statistics.Add(new TotalSlippage());


            // Create the BuySide strategy and add the imf instrument
            IMFStrat buySide = new IMFStrat(framework, "IMFStrat");
            buySide.AddInstruments(imfInstruments);
            buySide.SetParameter("Envelope", Threshold * Lambda);

            // Create the SellSide strategy, set Execution and Data provider
            InstrStrat sellSide = new InstrStrat(framework, "InstrStrat");
			buySide.ExecutionProvider = sellSide;
			buySide.DataProvider = sellSide;

            // Setup a main Strategy
            strategy = new Strategy(framework, "DCEMDTrading");
            strategy.AddStrategy(buySide);
            strategy.AddStrategy(sellSide);
            StartStrategy();

            // cleanup
            foreach (Instrument i in imfInstruments)
                InstrumentManager.Delete(i);
        }
    }
}








