namespace Intro_SomeGame
{
	// <example>
	public class SomeGameClass
	{
		public bool isRunning;
		public int counter;

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
