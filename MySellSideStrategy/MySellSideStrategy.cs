using System;
using System.Drawing;

using SmartQuant;

namespace OpenQuant
{
    public class InstrStrat : SellSideInstrumentStrategy
    {
        private TimeSeries maxs,mins,ovs,ovsDur,tm,tmDur;
        private TimeSeries maxsPred, minsPred;
        private Instrument imf;
        private Group quoteGroup;
        private Group dcBarsGroup;
        private Group equityGroup;
        private double lastQuotePrice;
        private Order imfOrder;
        private double lastImfPrice;
        private IMFPred imfPred;
        private double positionLimit = 10000;

        [Parameter]
        public double Lambda = 2.0;

        [Parameter]
        public int NrExtrema = 2;

        [Parameter]
        public int NrWarmup = 2;

        [Parameter]
        public double Cash = 20000;

        [Parameter]
        public bool DoSaveHFBars = false;


        public InstrStrat(Framework framework, string name)
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
            imfPred = new IMFPred(NrExtrema, Lambda / 10000.0);
            Portfolio.Account.Deposit(Cash, CurrencyId.USD, "inital deposit");
            AddGroups();
        }


        protected override void OnStrategyStart()
        {
            if (DataProvider.IsDisconnected)
                DataProvider.Connect(10);

            if (ExecutionProvider.IsDisconnected)
                ExecutionProvider.Connect(10);

        }

        protected override void OnSubscribe(Instrument instrument)
        {
            // Get spread instrument.
            imf = instrument;

            // Remove instruments from strategy.
            Instruments.Clear();

            foreach (Leg leg in imf.Legs)
                AddInstrument(leg.Instrument);

            Instrument = Instruments.GetByIndex(0);
        }

        #region Market data handeling
        // the bars are DC events with open being the ext and the close the dc
        protected override void OnBar(Instrument instrument, Bar bar)
        {
            if (bar.Type == BarType.Tick)
            {
                if (ExecutionProvider != ProviderManager.ExecutionSimulator)
                    Log(bar.CloseDateTime, bar.Close, quoteGroup);

                if (lastQuotePrice == bar.Close) return;

                Tick tick = new Tick(bar.CloseDateTime, 0, instrument.Id, bar.Close, 0);
                lastQuotePrice = tick.Price;

                if (Bars.Count >= NrWarmup)
                {
                    lastImfPrice = imfPred.PredictDC(tick);
                    EmitTrade(new Trade(tick.DateTime, 0, imf.Id, lastImfPrice, (int)positionLimit));
               
                }
                return;
            }

            if (bar.Type == BarType.Range)
            {
                Log(bar, dcBarsGroup);
                Bars.Add(bar);

                imfPred.OnDCBars(Bars);
                Log(bar.DateTime, Portfolio.Value, equityGroup);
                //positionLimit = Math.Round(Cash / bar.Close / 100.0, 0) * 100.0;
                positionLimit = framework.CurrencyConverter.Convert(Cash, CurrencyId.USD, instrument.CCY1);
            }       
        }
        #endregion

        #region Order handeling
        public override void OnSendCommand(ExecutionCommand command)
        {
            // Acknowlige the send command to the imf side;
            EmitIMFExecNew(command);

            // store the order
            imfOrder = command.Order;

            if (command.Side == OrderSide.Buy)
                Buy(Instrument, Math.Round(command.Qty,0) , "inst buy");
            else
                Sell(Instrument, Math.Round(command.Qty,0), "inst sell");
        }

        protected override void OnExecutionReport(ExecutionReport report)
        {
                if (report.ExecType != ExecType.ExecNew)
                    EmitIMFTraded(report);

            Portfolio.Performance.Update();
        }



        private void EmitIMFTraded(ExecutionReport report)
        {
            ExecutionReport buysideReport = new ExecutionReport();
            buysideReport.AvgPx = lastImfPrice;
            buysideReport.CumQty = report.CumQty;
            buysideReport.LastQty = report.LastQty;
            buysideReport.LeavesQty = report.LeavesQty;
            buysideReport.DateTime = report.DateTime;
            buysideReport.ExecType = report.ExecType;
            buysideReport.Instrument = imf;
            buysideReport.LastPx = buysideReport.AvgPx;

            buysideReport.Order = imfOrder;
            buysideReport.OrderId = imfOrder.Id;
            buysideReport.OrdType = imfOrder.Type;
            buysideReport.OrdQty = imfOrder.Qty;
            buysideReport.OrdStatus = report.OrdStatus;

            buysideReport.Price = imfOrder.Price;
            buysideReport.Side = imfOrder.Side;
            buysideReport.Text = imfOrder.Text;
            EmitExecutionReport(buysideReport);
        }

        private void EmitIMFExecNew(ExecutionCommand command)
        {
            Order order = command.Order;

            ExecutionReport report = new ExecutionReport();
            report.DateTime = Clock.DateTime;
            report.Order = order;
            report.Instrument = imf;
            report.OrdQty = order.Qty;
            report.ExecType = ExecType.ExecNew;
            report.OrdStatus = OrderStatus.New;
            report.OrdType = order.Type;
            report.Side = order.Side;
            EmitExecutionReport(report);
        }

        #endregion

        protected override void OnStrategyStop()
        {
            Console.WriteLine("{3}: Produced {0} HF Bars, from {1} to {2}", Bars.Count, Bars.FirstDateTime, Bars.LastDateTime, Instrument.Symbol);
            DataSeries existingRangeBars = DataManager.GetDataSeries(Instrument, DataObjectType.Bar, BarType.Range, (long)Lambda);
            if (DoSaveHFBars)
            {
                if (existingRangeBars != null)
                    DataManager.DeleteDataSeries(existingRangeBars.Name);

                DataManager.Dump();
                DataManager.Save(Bars, SaveMode.Add);
            }
        }

        private void AddGroups()
        {
            // Create bars group.
            dcBarsGroup = new Group("DC-Bars");
            dcBarsGroup.Add("Format", Instrument.PriceFormat);
            dcBarsGroup.Add("Pad", DataObjectType.String, 0);
            dcBarsGroup.Add("SelectorKey", Instrument.Symbol);
            dcBarsGroup.Add("ChartStyle", "Bar");

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


            // Add groups to manager.
            GroupManager.Add(dcBarsGroup);
            GroupManager.Add(quoteGroup);
            GroupManager.Add(equityGroup);
        }


        private void PredMLE(Bar bar)
        {
            maxsPred = new TimeSeries();
            minsPred = new TimeSeries();

            int nrMaxs = maxs.Count;
            int nrMins = mins.Count;

            for (int i = NrExtrema; i >= 1; i--)
            {
                maxsPred.Add(maxs.GetDateTime(nrMaxs - i), maxs[nrMaxs - i]);
                minsPred.Add(mins.GetDateTime(nrMins - i), mins[nrMins - i]);
            }

            TimeSeries v = ovs.GetPositiveSeries();
            double ovsEst = Math.Exp(v.Log().GetMean()) * v.Count / ovs.Count;
            v = ovsDur.GetPositiveSeries();
            double ovsDurEst = Math.Exp(v.Log().GetMean()) * v.Count / ovsDur.Count;

            double tmEst = Math.Exp(tm.Log().GetMean());
            double tmDurEst = Math.Exp(tmDur.Log().GetMean());


            // upwards move
            if (bar.Close > bar.Open)
            {
                maxsPred.Add(bar.CloseDateTime.AddMilliseconds(ovsDurEst), bar.Close + ovsEst);
                minsPred.Add(maxsPred.LastDateTime.AddMilliseconds(tmDurEst), maxsPred.Last - tmEst);

                for (int i = 1; i < NrExtrema; i++)
                {
                    maxsPred.Add(maxsPred.LastDateTime.AddMilliseconds(2 * tmDurEst), maxsPred.Last);
                    minsPred.Add(minsPred.LastDateTime.AddMilliseconds(2 * tmDurEst), minsPred.Last);
                }
            }
            // downwards move
            else
            {
                minsPred.Add(bar.CloseDateTime.AddMilliseconds(ovsDurEst), bar.Close - ovsEst);
                maxsPred.Add(minsPred.LastDateTime.AddMilliseconds(tmDurEst), minsPred.Last + tmEst);

                for (int i = 1; i < NrExtrema; i++)
                {
                    minsPred.Add(minsPred.LastDateTime.AddMilliseconds(2 * tmDurEst), minsPred.Last);
                    maxsPred.Add(maxsPred.LastDateTime.AddMilliseconds(2 * tmDurEst), maxsPred.Last);
                }
            }
        }
    }
}


