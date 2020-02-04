namespace SomeGame
{
	#pragma warning disable CS0649 // Field 'SomeGameClass.isRunning' is never assigned to, and will always have its default value false

	// <example>
	public class SomeGameClass
	{
		private bool isRunning;
		private int counter;

		private int DoSomething()
		{
			if (isRunning)
			{
				counter++;
			}
			return counter * 10;
		}
	}
	// </example>
}
