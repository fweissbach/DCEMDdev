using System;

using SmartQuant;

namespace OpenQuant
{
	class Program
	{
		static void Main(string[] args)
		{
			Scenario scenario = new liveCNX(Framework.Current);

			scenario.Run();
		}
	}
}
