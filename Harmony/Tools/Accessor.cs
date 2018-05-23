using Harmony;
using System;

namespace Harmony
{
	public class Reference<T>
	{
		private Traverse traverse;

		public Reference(Traverse traverse)
		{
			this.traverse = traverse;
		}

		public T Value
		{
			get { return traverse.GetValue<T>(); }
			set { traverse.SetValue(value); }
		}
	}

	public class Accessor
	{
		private Traverse traverse;

		public Accessor(object instance)
		{
			traverse = new Traverse(instance);
		}

		public Accessor(Type type)
		{
			traverse = new Traverse(type);
		}

		public static Accessor ForInstance(object instance)
		{
			return new Accessor(instance);
		}

		public static Accessor ForType(Type type)
		{
			return new Accessor(type);
		}

		public Reference<T> Field<T>(string fieldName)
		{
			return new Reference<T>(traverse.Field(fieldName));
		}

		public Reference<T> Property<T>(string propertyName)
		{
			return new Reference<T>(traverse.Property(propertyName));
		}
	}

}
