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

        string symbols = "EURUSD";
        DateTime startDateTime = 	new DateTime(2019, 6, 25, 7, 15, 0);
        DateTime endDateTime = 		new DateTime(2019, 6, 25, 8, 40, 0);
        long Lambda = 10;
        double Threshold = 0.67;
        double Cash = 10000;
        bool tradingDisabled = true; // set to true for trading sessions

        // closeMode = 0:  cross of 0 closes the position
        // closeMode = 1:  long:crossFromBelow(envU) and short:crossFromAbove(envL) closes trade
        // closeMode = 2:  crossFromAbove(envU) and crossFromBelow(envL) closes trade
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
				BarFactory.Add(instrument, BarType.Tick, 1, BarInput.Middle);
                instruments.Add(instrument);
            }

            // settings for quotes
            this.SetupForQuoteStream();
            framework.CurrencyConverter = new MyCurrencyConverter(this, instruments, false);

            // Event filter for session time
           //EventManager.Filter = new SessionEventFilter(framework, sessionStart, sessionEnd);

            // FillOnBar will simulate mid market trading
            // FillOnQuote generates slippage
            ExecutionSimulator.FillOnBar = true;
            ExecutionSimulator.FillOnQuote = true;

            DataSimulator.DateTime1 = startDateTime;
            DataSimulator.DateTime2 = endDateTime;

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

            StartStrategy();
        }
    }

}











