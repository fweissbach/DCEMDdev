using System;
using System.Collections.Generic;
using System.Linq;
//using System.Text;
using System.Threading.Tasks;
using System.IO;
using SmartQuant;

namespace OpenQuant
{
    public class DukascopyTickImportScenario : Scenario
    {
        bool doSaveInOpenQuant = true;

        public DukascopyTickImportScenario(Framework framework)
            : base(framework)
        {
        }

		public override void Run()
		{

			ProviderId.Add("Dukascopy", 100);
			// We strat from a 3+3 character symbol
			// 1) extract the base ccy, 2) select or add the instrument
			string[] symbols =  {
				"AUDJPY",
				"AUDUSD",
				"EURCHF",
				"EURGBP",
				"EURJPY",
				"EURNOK",
				"EURSEK",
				"EURUSD",
				"GBPUSD",
				"NZDUSD",
				"USDCAD",
				"USDCHF",
				"USDJPY"};

			string[] files = Directory.GetFiles(@"M:\3. Users\Frank\Data", "*.csv",System.IO.SearchOption.AllDirectories);

			foreach (string symbol in symbols)
			{
				string ccy1 = symbol.Substring(0, 3);
				string ccy2 = symbol.Substring(3, 3);
				Instrument instrument = InstrumentManager.Instruments[symbol];

				if (instrument == null)
				{
					instrument = new Instrument(InstrumentType.FX, symbol, symbol + " new", CurrencyId.GetId(ccy2));
					instrument.PriceFormat = "F5";
					instrument.CCY2 = CurrencyId.GetId(ccy2);
                    // add extra info for Currenex
                    AltId altId = new AltId(ProviderId.Currenex, symbol.Insert(3,"/"), "CNX", CurrencyId.GetId(ccy1));
                    instrument.AltId.Add(altId);

					InstrumentManager.Add(instrument, true);
				}

                DateTime datetime1 = new DateTime(2020,02,12,09,00,00);
				DateTime datetime2 = new DateTime(2020,02,20,11,00,00);

				//DataSeries dataseriesTest = DataManager.GetDataSeries(instrument, DataObjectType.Quote);

                //DataSeries dataseries;
                //if (dataseriesTest == null)
                //    dataseries = DataManager.AddDataSeries(instrument, DataObjectType.Quote);
                //else
                //    dataseries = dataseriesTest;
                  

				foreach (string file in files.Where(t => t.Contains(symbol) && t.Contains("Ticks")))
				{
					int i = file.LastIndexOf("_");
					// yyyy.mm.dd_yyyy.mm.dd
					//  9876543210123456789
					DateTime fileDateTime1 = new DateTime(int.Parse(file.Substring(i - 10, 4)), int.Parse(file.Substring(i - 5, 2)), int.Parse(file.Substring(i - 2, 2)));
					DateTime fileDateTime2 = new DateTime(int.Parse(file.Substring(i + 1, 4)), int.Parse(file.Substring(i + 6, 2)), int.Parse(file.Substring(i + 9, 2)));
					if (fileDateTime1 <= datetime2.Date && fileDateTime2 >= datetime1.Date)
					{
                        QuoteSeries quotes = Util.DukascopyQuoteSeries(file, instrument);
						if (quotes != null)
						{
							Console.WriteLine("Adding {0} quotes {1}-{2} from file {3}", quotes.Count, quotes.FirstDateTime, quotes.LastDateTime, file);

							if (doSaveInOpenQuant)
							{
								foreach (Quote quote in quotes)
								{
                                    DataManager.Save(quote, SaveMode.Add);
								}
							}
						}
					}
				}
			}
			framework.Dispose();
		}

  

    }
}





























