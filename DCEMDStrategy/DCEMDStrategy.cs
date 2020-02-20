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
        private Order currOrder;
		private string orderFile, orderSession;
		public byte BaseCcy = CurrencyId.USD;

		//private bool Locked;
		
		[Parameter]
		public bool UseStopLoss = false;

        
		[Parameter]
		public bool OrderLockedOverride = false;
		// To avoid duplicate trades whilean order has not been confirmed yet

		[Parameter]
		public bool InSession = true;

		[Parameter]
		public double Lambda = 10.0;

		[Parameter]
		public double ItmLimitBPS = 0.25;

		[Parameter]
		public double SLlevel = 105.0;

		[Parameter]
		public double PositionLimit;

		[Parameter]
		public int NrExtrema = 2;

		[Parameter]
		public int NrWarmup = 4;

		[Parameter]
		public double Cash = 25000;

		[Parameter]
		public double Envelope;

		[Parameter]
		public int CloseMode = 1;

		[Parameter]
		public TimeSpan SessionStart = new TimeSpan(01, 15, 00);

		[Parameter]
		public TimeSpan SessionEnd = new TimeSpan(21, 58, 0);


		public DCEMDStrategy(Framework framework, string name)
			: base(framework, name)
		{
		}

		protected override void OnStrategyInit()
		{
			// test branch
			maxs = new TimeSeries(); mins = new TimeSeries();
			ovs = new TimeSeries(); ovsDur = new TimeSeries();
			tm = new TimeSeries(); tmDur = new TimeSeries();
			maxsPred = new TimeSeries(); minsPred = new TimeSeries();
			lastQuotePrice = 0.0;
			imf = new TimeSeries();
			imfPred = new IMFPred(NrExtrema, Lambda / 10000.0);
			orderFile = @"M:\3. Users\Frank\Data\" + DateTime.Now.ToString("yyyyMMdd_hhmm") + Mode.ToString()+@".csv";
			orderSession = DateTime.Now.ToString("yyyyMMdd_hhmm") + Mode.ToString();
			AddGroups();
		}

		protected override void OnStrategyStart()
		{
			PositionLimit = Math.Round(framework.CurrencyConverter.Convert(Cash, BaseCcy, Instrument.CCY1) / 100.0, 0) * 100.0;
			// add reminder for EO Trading date
			// Start the Strategy during a session
			Portfolio.Account.Deposit(PositionLimit, Instrument.CCY1, "inital deposit",true);
			InSession = true;
         
			// AddReminder(Clock.DateTime.Date.Add(SessionEnd));
		}

		protected override void OnBar(Instrument instrument, Bar bar)
		{
			switch (bar.Volume)
			{
				case -999:
				{
                        

					// log if not in simulation mode
					if (Mode != StrategyMode.Backtest)
						Log(bar.CloseDateTime, bar.Close, quoteGroup);

					// storing the mid
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
						//   PositionLimit = Math.Round(framework.CurrencyConverter.Convert(Cash, CurrencyId.USD, instrument.CCY1) / 100.0, 0) * 100.0;
					}
					return;
				}
				default:
				{
					Portfolio.Performance.Update();

					if (Mode != StrategyMode.Backtest)
						Log(bar, dcBarsGroup);

					Bars.Add(bar);

					imfPred.OnDCBars(Bars);

					Log(bar.DateTime, Portfolio.Value, equityGroup);

					return;
				}
			}
		}


		private void OnIMFTick(Tick imfTick)
		{
			if (Mode != StrategyMode.Backtest)
				Log(imfTick.DateTime, imfTick.Price, imfGroup);


			imf.Add(imfTick.DateTime, imfTick.Price);

			double envU = Envelope;
			double envL = -Envelope;

			if (Mode != StrategyMode.Backtest)
			{
				Log(imfTick.DateTime, envU, envUGroup);
				Log(imfTick.DateTime, envL, envLGroup);
			}

			int index = imf.Count - 1;

            // InSession controlled by user
            // out of session before & after midnight
            // typical after 22:00 andd before 01:15
			if (!InSession || imfTick.DateTime.TimeOfDay > SessionEnd || imfTick.DateTime.TimeOfDay < SessionStart)
				return;

			if (Bars.Count <= NrWarmup)
				return;

            if (IsLockedOrder(currOrder))
                return;

            // Compact trade logic
            // if there is a long/short postion then close it if the opposite envelope or 0 is crossed
            // if there is no position then open it if the envelope is crosses
            Cross highCross = imf.Crosses(envU, index);
			Cross lowCross = imf.Crosses(envL, index);
			Cross zeroCross = imf.Crosses(0.0, index);


			// handling of Long position
			if (HasLongPosition())
			{
				if (zeroCross == Cross.Above && CloseMode == 0)
				{
					SendLimitOrder(OrderSide.Sell, Position.Qty, "closing@zero trade");
				}
				else if (highCross == Cross.Above && CloseMode == 1)
				{
					SendLimitOrder(OrderSide.Sell, Position.Qty, "closing@envU trade");
				}
				else if (UseStopLoss && imfTick.Price < -SLlevel)
				{
					SendLimitOrder(OrderSide.Sell, Position.Qty, "Stoploss hit");
                    OnStopHit();
                }
			}

			// handling of Short position
			else if (HasShortPosition())
			{
				if (zeroCross == Cross.Below && CloseMode == 0)
				{
					SendLimitOrder(OrderSide.Buy, Position.Qty, "closing@zero trade");
				}
				else if (lowCross == Cross.Below && CloseMode == 1)
				{
					SendLimitOrder(OrderSide.Buy, Position.Qty, "closing@envL trade");
				}
				else if (UseStopLoss && imfTick.Price > SLlevel)
				{
					SendLimitOrder(OrderSide.Buy, Position.Qty, "Stoploss hit");
                    OnStopHit();
				}
			}
				// handling of No position
			else
			{
				if (lowCross == Cross.Above)
				{
					SendLimitOrder(OrderSide.Buy, PositionLimit, "opening trade");
				}
				else if (highCross == Cross.Below)
				{
					SendLimitOrder(OrderSide.Sell, PositionLimit, "opening trade");
				}
			}
		}

        protected Boolean IsLockedOrder(Order order)
        {
            if (order == null || OrderLockedOverride)
                return false;


            if (order.IsDone || order.IsRejected || order.IsCancelled)
                return false;
            else
                return true;
        }

		protected void SendLimitOrder(OrderSide side,double qty, String text)
		{

			double ItmLimitFactor = 1.0 + ItmLimitBPS / 10000.0;
			if (side == OrderSide.Buy)
				currOrder = BuyLimitOrder(Instrument, qty, Math.Round(lastQuotePrice * ItmLimitFactor,(int)Instrument.TickSize), text);
			else
                currOrder = SellLimitOrder(Instrument, qty, Math.Round(lastQuotePrice / ItmLimitFactor, (int)Instrument.TickSize), text);

            currOrder.TimeInForce = TimeInForce.FOK;

            // Avoiding partial fills (Currenex documentation); 
            currOrder.MinQty = currOrder.Qty;

			Send(currOrder);
		}

        protected void OnStopHit()
        {
            DateTime resstartDT = Clock.DateTime.AddMinutes(1440);
            Console.WriteLine(Clock.DateTime.ToString() + ":" + Instrument.ToString() + " StopLossHit trading paused untill " + resstartDT.ToString());
            InSession = false;
            AddReminder(resstartDT);
        }

        protected override void OnReminder(DateTime dateTime, object data)
        {
            Console.WriteLine(Clock.DateTime.ToString() + ":" + Instrument.ToString() + " trading resumes");
            InSession = true;
        }


        protected override void OnOrderDone(Order order)
		{
			//Util.SaveOrdersCSV(orderFile, order);
			if (Mode != StrategyMode.Backtest)
				Util.SaveOrdersMySQL(orderSession, order);
             
			base.OnOrderDone(order);   
		}

		protected override void OnExecutionReport(ExecutionReport report)
		{
			if (Mode == StrategyMode.Live)
				report.Commission = ExecutionSimulator.CommissionProvider.GetCommission(report);
		}


        protected override void OnPositionChanged(Position position)
        {
            //	if (Mode != StrategyMode.Backtest)
            {
                Log(position.Amount, "Position");
                Log(position.UPnL, "uPnL");
            }
            base.OnPositionChanged(position);
        }


		protected override void OnFill(Fill fill)
		{
			Log(fill, fillGroup);
			Fill imfFill = new Fill(fill.DateTime, new Order(), Instrument, Instrument.CurrencyId,  fill.Side, fill.Qty, lastImfPrice);
			if (Mode != StrategyMode.Backtest)
				Log(imfFill, imfFillGroup);
			
			base.OnFill(fill);
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

			Group("Tick", "LogName", "Tick");
			Group("Tick", "StrategyName", this.Name);
			Group("Tick", "Symbol", Instrument.Symbol);

			Group("IMF", "LogName", "IMF");
			Group("IMF", "StrategyName", this.Name);
			Group("IMF", "Symbol", Instrument.Symbol);

			Group("Position", "LogName", "Position");
			Group("Position", "StrategyName", this.Name);
			Group("Position", "Symbol", Instrument.Symbol);

			Group("uPnL", "LogName", "uPnL");
			Group("uPnL", "StrategyName", this.Name);
			Group("uPnL", "Format", "F1");
			Group("uPnL", "Symbol", Instrument.Symbol);


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






