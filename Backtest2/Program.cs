using System;

using SmartQuant;

namespace OpenQuant
{
	class Program
	{
		static void Main(string[] args)
		{
			Scenario scenario = new Backtest2(Framework.Current);

			scenario.Run();
		}
	}
}
