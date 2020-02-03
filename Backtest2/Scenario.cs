using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SmartQuant;

namespace OpenQuant
{
    public class Backtest2 : Scenario
    {

        // string symbols = "EURUSD,EURCHF,USDJPY,GBPUSD,EURGBP,USDCHF";
		// string symbols = "AUDJPY,AUDUSD,EURCHF,EURGBP,EURJPY,EURNOK,EURSEK,EURUSD,GBPUSD,NZDUSD,USDCAD,USDCHF,USDJPY";

		string symbols = "AUDJPY,AUDUSD,EURCHF,EURGBP,EURJPY,EURNOK,EURSEK,EURUSD,GBPUSD,NZDUSD,USDCAD,USDCHF,USDJPY";
        DateTime startDateTime = 	new DateTime(2019, 10, 2, 3, 0, 0);
        DateTime endDateTime = 		new DateTime(2019, 12, 29, 5, 0, 0);
//		DateTime endDateTime = 		new DateTime(2019, 12, 27, 18, 00, 0);
		
		long Lambda = 10;
        double Threshold = 0.67;
        double Cash = 100000;
        bool UseStopLoss = false;  // IMF based StopLoss
        double SLlevel = 20.0; // IMF base stoploss level

        // closeMode = 0:  cross of 0 closes the position
        // closeMode = 1:  long:crossFromBelow(envU) and short:crossFromAbove(envL) closes trade
        int CloseMode = 1;

        public Backtest2(Framework framework)
            : base(framework)
        {
        }

        public override void Run()
        {

            
            // setup the instruments:
            // A 1 tick barfactory drives the datastream to reduce the eventflow
            // Direction changes are detected and emitted by the DCBarFactoryItem
            InstrumentList instruments = new InstrumentList();
            foreach (string symbol in symbols.Split(','))
            {
                Instrument instrument = InstrumentManager.Instruments[symbol];

                BarFactory.Add(new DCBarFactoryItem(instrument, Lambda));
                BarFactory.Add(new ChangeFactoryItem( instrument, 50));
			//	BarFactory.Add(instrument, BarType.Range, 1, BarInput.Middle);
                instruments.Add(instrument);
            }

            
            // settings for quotes
            this.SetupForHistoricalTicks();

            framework.CurrencyConverter = new MyCurrencyConverter(this, instruments, true,CurrencyId.USD);

            // Event filter for session time
           //EventManager.Filter = new SessionEventFilter(framework, sessionStart, sessionEnd);

            // FillOnBar will simulate mid market trading
            // FillOnQuote generates slippage
            ExecutionSimulator.FillOnBar = true;
            ExecutionSimulator.FillOnQuote = false;
            ExecutionSimulator.PartialFills = false;

           // Set event filter.
           // EventManager.Filter = new IBForexFilter(framework);

          //  IBCommission ibCommission = new IBCommission();
          //  ExecutionSimulator.CommissionProvider = ibCommission;


            DataSimulator.DateTime1 = startDateTime;
            DataSimulator.DateTime2 = endDateTime;

          //  StatisticsManager.Statistics.Add(new TotalSlippageQ());
          //  StatisticsManager.Statistics.Add(new SlippageBps());
          //  StatisticsManager.Statistics.Add(new TotalSlippage());


            // Setup a main Strategy
            strategy = new DCEMDStrategy(framework, "DCEMD backtest");
            strategy.AddInstruments(instruments);
            strategy.SetParameter("Envelope", Threshold * Lambda);
            strategy.SetParameter("Lambda", Lambda);
            strategy.SetParameter("Cash", Cash);
            strategy.SetParameter("CloseMode", CloseMode);
            strategy.SetParameter("UseStopLoss", UseStopLoss);
            strategy.SetParameter("SLlevel", SLlevel);

            StartStrategy(StrategyMode.Backtest);
        }
    }

}




