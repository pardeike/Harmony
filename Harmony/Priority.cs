using System;

namespace Harmony
{
	public static class Priority
	{
		public static Value Last = new Value(0);
		public static Value VeryLow = new Value(100);
		public static Value Low = new Value(200);
		public static Value LowerThanNormal = new Value(300);
		public static Value Normal = new Value(400);
		public static Value HigherThanNormal = new Value(500);
		public static Value High = new Value(600);
		public static Value VeryHigh = new Value(700);
		public static Value First = new Value(800);

		public static Value Plus(this Value self, int value)
		{
			return new Value(self.value + value);
		}

		public static Value Minus(this Value self, int value)
		{
			return new Value(self.value - value);
		}

		public class Value : IComparable
		{
			public int value;

			public Value(int value)
			{
				this.value = value;
			}

			public override bool Equals(object obj)
			{
				return ((obj != null) && (obj is Value) && (value == ((Value)obj).value));
			}

			public int CompareTo(object obj)
			{
				var other = obj as Value;
				return value.CompareTo(other.value);
			}

			public override int GetHashCode()
			{
				return value.GetHashCode();
			}
		}

		public static Value For(int priority)
		{
			return new Value(priority);
		}
	}
}