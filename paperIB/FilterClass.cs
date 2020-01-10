using System;
using System.Collections.Generic;
using SmartQuant;

namespace OpenQuant
{
	public class Level2EventFilter : EventFilter
	{
		public Level2EventFilter(Framework framework)
			: base(framework)
		{
			//Global
		}

		public override Event Filter(Event e)
		{
			try
			{
				switch (e.TypeId)
				{
					case DataObjectType.Level2:
					case DataObjectType.Level2Snapshot:
					case DataObjectType.Level2Update:

						return null;
				}
				return e;
			}
			catch (Exception ex)
			{
				Console.WriteLine("Error in MyEventsFilter. Error is: {0}", ex.Message);
				return null;
			}
		}
	}
}


