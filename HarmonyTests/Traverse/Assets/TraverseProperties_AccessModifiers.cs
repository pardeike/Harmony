using System;
using System.Linq;

namespace HarmonyLibTests.Assets
{
#pragma warning disable CS0414

	public class TraverseProperties
	{
		public static string[] testStrings = ["test01", "test02", "test03", "test04", "test05", "test06", "test07"];
		public static string[] propertyNames = ["PublicProperty", "PublicPrivateProperty", "AutoProperty", "BaseProperty1", "BaseProperty2", "BaseProperty3", "ImmediateProperty"];
	}

	public class Traverse_ExtraClass(string val)
	{
		public readonly string someString = val;
		public readonly Traverse_BaseClass baseClass = new();
	}

	public class Traverse_BaseClass
	{
		string _basePropertyField1;
		protected virtual string BaseProperty1
		{
			get => _basePropertyField1;
			set => _basePropertyField1 = value;
		}

		string _basePropertyField2;
		protected virtual string BaseProperty2
		{
			get => _basePropertyField2;
			set => _basePropertyField2 = value;
		}

		public string BaseProperty3
		{
			get => throw new Exception();
			set => throw new Exception();
		}

		static readonly string staticField = "test1";
		private readonly string baseField = "base-field";

		static string StaticProperty => "test1";
		private string BaseProperty => "base-property";

		private string BaseMethod() => "base-method";
	}

	public static class TraverseFields_Static
	{
		static readonly string staticField = "test2";
		public static readonly Traverse_ExtraClass extraClassInstance = new("test2");
	}

	public static class TraverseProperties_Static
	{
		static string StaticProperty => "test2";
	}

	public class TraverseProperties_AccessModifiers : Traverse_BaseClass
	{
		string _publicPropertyField;
		public string PublicProperty
		{
			get => _publicPropertyField;
			set => _publicPropertyField = value;
		}

		string _publicPrivatePropertyField;
		public string PublicPrivateProperty
		{
			get => _publicPrivatePropertyField;
			set => _publicPrivatePropertyField = value;
		}

		string AutoProperty { get; set; }

		protected override string BaseProperty1
		{
			get => base.BaseProperty1;
			set => base.BaseProperty1 = value;
		}

		// BaseProperty2 defined and used in base class

		string _basePropertyField3;
		public new string BaseProperty3
		{
			get => _basePropertyField3;
			set => _basePropertyField3 = value;
		}

		string ImmediateProperty => TraverseProperties.testStrings.Last();

		public TraverseProperties_AccessModifiers(string[] s)
		{
			PublicProperty = s[0];
			PublicPrivateProperty = s[1];
			AutoProperty = s[2];
			BaseProperty1 = s[3];
			BaseProperty2 = s[4];
			BaseProperty3 = s[5];
			// immediateProperty is readonly
		}

		public string GetTestProperty(int n)
		{
			switch (n)
			{
				case 0:
					return PublicProperty;
				case 1:
					return PublicPrivateProperty;
				case 2:
					return AutoProperty;
				case 3:
					return BaseProperty1;
				case 4:
					return BaseProperty2;
				case 5:
					return BaseProperty3;
				case 6:
					return ImmediateProperty;
				default:
					return null;
			}
		}

		public void SetTestProperty(int n, string value)
		{
			switch (n)
			{
				case 0:
					PublicProperty = value;
					break;
				case 1:
					PublicPrivateProperty = value;
					break;
				case 2:
					AutoProperty = value;
					break;
				case 3:
					BaseProperty1 = value;
					break;
				case 4:
					BaseProperty2 = value;
					break;
				case 5:
					BaseProperty3 = value;
					break;
				case 6:
					// ImmediateProperty = value;
					break;
			}
		}

		public override string ToString() => "TraverseProperties_AccessModifiers";
	}

	public class TraverseProperties_SubClass : Traverse_BaseClass
	{
	}

#pragma warning restore CS0414
}
