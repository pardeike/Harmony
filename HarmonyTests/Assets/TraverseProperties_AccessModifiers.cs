using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HarmonyTests.Assets
{
	public class TraverseProperties
	{
		public static string[] testStrings = new string[] { "test01", "test02", "test03", "test04", "test05", "test06", "test07" };
		public static string[] propertyNames = new string[] { "publicProperty", "publicPrivateProperty", "autoProperty", "baseProperty1", "baseProperty2", "baseProperty3", "immediateProperty" };
	}

	public class TraverseProperties_BaseClass
	{
		string _basePropertyField1;
		protected virtual string baseProperty1
		{
			get { return _basePropertyField1; }
			set { _basePropertyField1 = value; }
		}

		string _basePropertyField2;
		protected virtual string baseProperty2
		{
			get { return _basePropertyField2; }
			set { _basePropertyField2 = value; }
		}

		public string baseProperty3
		{
			get { throw new Exception(); }
			set { throw new Exception(); }
		}
	}

	public class TraverseProperties_AccessModifiers : TraverseProperties_BaseClass
	{
		private string _publicPropertyField;
		public string publicProperty
		{
			get { return _publicPropertyField; }
			set { _publicPropertyField = value; }
		}

		private string _publicPrivatePropertyField;
		public string publicPrivateProperty
		{
			get { return _publicPrivatePropertyField; }
			private set { _publicPrivatePropertyField = value; }
		}

		string autoProperty { get; set; }

		protected override string baseProperty1
		{
			get { return base.baseProperty1; }
			set { base.baseProperty1 = value; }
		}

		// baseProperty2 defined and used in base class

		string _basePropertyField3;
		public new string baseProperty3
		{
			get { return _basePropertyField3; }
			set { _basePropertyField3 = value; }
		}

		string immediateProperty => TraverseProperties.testStrings.Last();

		public TraverseProperties_AccessModifiers(string[] s)
		{
			publicProperty = s[0];
			publicPrivateProperty = s[1];
			autoProperty = s[2];
			baseProperty1 = s[3];
			baseProperty2 = s[4];
			baseProperty3 = s[5];
			// immediateProperty is readonly
		}

		public string GetTestProperty(int n)
		{
			switch (n)
			{
				case 0:
					return publicProperty;
				case 1:
					return publicPrivateProperty;
				case 2:
					return autoProperty;
				case 3:
					return baseProperty1;
				case 4:
					return baseProperty2;
				case 5:
					return baseProperty3;
				case 6:
					return immediateProperty;
			}
			return null;
		}

		public void SetTestProperty(int n, string value)
		{
			switch (n)
			{
				case 0:
					publicProperty = value;
					break;
				case 1:
					publicPrivateProperty = value;
					break;
				case 2:
					autoProperty = value;
					break;
				case 3:
					baseProperty1 = value;
					break;
				case 4:
					baseProperty2 = value;
					break;
				case 5:
					baseProperty3 = value;
					break;
				case 6:
					// immediateProperty = value;
					break;
			}
		}

		public override string ToString()
		{
			return "TraverseProperties_AccessModifiers";
		}
	}
}