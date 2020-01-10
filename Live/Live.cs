using System;
using SmartQuant;
using SmartQuant.IB;

namespace OpenQuant
{
    public class Live : Scenario
    {
        string symbols = "EURUSD,GBPUSD,USDJPY,EURGBP";
        long Lambda = 2;
        double Threshold = 0.66; // as fraction of Lambda
        int warmupDays = 2;

        public Live(Framework framework)
            : base(framework)
        {
		}

        public override void Run()
        {
            // Prepare running.
            Console.WriteLine("Prepare running in {0} mode...", StrategyManager.Mode);

            IProvider provider = ProviderManager.GetProvider("QuantRouter");
            if (provider.Status == ProviderStatus.Disconnected)
                provider.Connect(10);

            DateTime warmupEnd = DateTime.Now;
            DateTime warmupStart = warmupEnd.AddBusinessDays(-warmupDays);

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

                //BarSeries barSeries = DataManager.GetHistoricalBars((IHistoricalDataProvider)ibtws, instrument, warmupStart, warmupEnd, BarType.Time, 60);
                //if (barSeries.Count > 0)
                //{
                //    QuoteSeries qs = new QuoteSeries();
                //    qs.Add(new Quote(barSeries.FirstDateTime, 0, instrument.Id, barSeries.First.Open, 100, barSeries.First.Open, 100));

                //    foreach (Bar bar in barSeries)
                //    {
                //        double c = qs[qs.Count - 1].Bid.Price;
                //        double tp1 = (bar.High - c < c - bar.Low) ? bar.High : bar.Low;
                //        double tp2 = (tp1 == bar.High) ? bar.Low : bar.High;
                //        qs.Add(new Quote(qs.LastDateTime.AddSeconds(20), 0, instrument.Id, tp1, 100, tp1, 100));
                //        qs.Add(new Quote(qs.LastDateTime.AddSeconds(20), 0, instrument.Id, tp2, 100, tp2, 100));
                //        qs.Add(new Quote(bar.CloseDateTime, 0, instrument.Id, bar.Close, 100, bar.Close, 100));
                //    }

                //    Console.WriteLine("qs :" + qs.ToString());
                //    DataSimulator.Series.Add(qs);

                //}
                //else
                //{
                //    Console.WriteLine("**********************");
                //    Console.WriteLine("Unable to get data for warmup");
                //    Console.WriteLine("**********************");
                //}
            }

            // Create the BuySide strategy and add the imf instrument
            IMFStrat buySide = new IMFStrat(framework, "BuySide");
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

            StatisticsManager.Statistics.Add(new TotalSlippageQ());
            StatisticsManager.Statistics.Add(new SlippageBps());
            StatisticsManager.Statistics.Add(new TotalSlippage());

            //// Warmup stage
            //Console.WriteLine("Run in Backtest mode.");
            //StrategyManager.Mode = StrategyMode.Backtest;
            //IMFStrat.TradingDisabled = true;

            //this.SetupForDataSimulatorSeries();
            //DataSimulator.DateTime1 = warmupStart;
            //DataSimulator.DateTime2 = warmupEnd;
            //StartStrategy(StrategyMode.Backtest);

            ////Live stage
            Console.WriteLine("Run in Live mode.");
            StrategyManager.Mode = StrategyMode.Live;
            sellSide.ExecutionProvider = provider as IExecutionProvider;
            sellSide.DataProvider = provider as IDataProvider;
            IMFStrat.TradingDisabled = false;
            framework.CurrencyConverter = new MyCurrencyConverter(this, instruments, false);
            StartStrategy(StrategyMode.Live);


            // cleanup
            foreach (Instrument i in imfInstruments)
                InstrumentManager.Delete(i);


        }
    }
}

