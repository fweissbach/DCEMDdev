using System;
using SmartQuant;

namespace OpenQuant
{
	class Program
	{
		static void Main(string[] args)
		{
			Scenario scenario = new DukascopyTickImportScenario(Framework.Current);

			scenario.Run();

            Framework.Current.Dispose();
		}
	}
}
