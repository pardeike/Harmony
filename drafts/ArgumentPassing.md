# ABOUT

A draft of how different arguments are passed and used.

# CONCEPT

Pseudo code to demonstrate the cases:


```cs
var obj = new SomeObject();
var val = new SomeValue { n = 999 };
Original_Object(obj);
Original_Value(val);
Original_Object_Ref(ref obj);
Original_Value_Ref(ref val);
Console.WriteLine(val.n);

public class SomeObject { }
public struct SomeValue { public int n; }

static void Original_Object(SomeObject p)
{
	Patch_Object(p);
	// Patch_Value(p);                          class->value not possible
	// Patch_Boxing(p);                         not needed, same as Patch_Object()
	Patch_Object_Ref(ref p);
	// Patch_Value_Ref(ref p);                  class->ref_value not possible
	// Patch_Boxing_Ref(p);                     not needed, same as Patch_Object_Ref()
}

static void Original_Value(SomeValue p)
{
	// Patch_Object(p);                         value->class not possible
	Patch_Value(p);
	Patch_Boxing(p);
	//Patch_Object_Ref(ref p);                  value->ref_class not possible
	Patch_Value_Ref(ref p);
	var o = (object)p; Patch_Boxing_Ref(ref o);
}

static void Original_Object_Ref(ref SomeObject p)
{
	Patch_Object(p);
	// Patch_Value(p);                          ref_class->value not possible
	// Patch_Boxing(p);                         not needed, same as Patch_Object()
	Patch_Object_Ref(ref p);
	// Patch_Value_Ref(ref p);                  class->ref_value not possible
	// Patch_Boxing_Ref(p);                     not needed, same as Patch_Object_Ref()
}

static void Original_Value_Ref(ref SomeValue p)
{
	// Patch_Object(p);                         ref_value->class not possible
	Patch_Value(p);
	Patch_Boxing(p);
	//Patch_Object_Ref(ref p);                  ref_value->ref_class not possible
	Patch_Value_Ref(ref p);
	var o = (object)p; Patch_Boxing_Ref(ref o); p = (SomeValue)o;
}

//

static void Patch_Object(SomeObject p) { Debug(p.GetType());  }
static void Patch_Value(SomeValue p) { Debug(p.GetType());  }
static void Patch_Boxing(object p) { Debug(p.GetType());  }

static void Patch_Object_Ref(ref SomeObject p) { Debug(p.GetType());  }
static void Patch_Value_Ref(ref SomeValue p) { Debug(p.GetType());  }
static void Patch_Boxing_Ref(ref object p) { Debug(p.GetType()); Traverse.Create(p).Field("n").SetValue(123);  }

static void Debug(Type t)
{
	var f = new StackTrace(true).GetFrames();
	var str = f[2].GetMethod().Name.Replace("Original_", "") + ":" + f[1].GetMethod().Name.Replace("Patch_", "");
	Console.WriteLine(str +  " -> " + t.Name.Replace("Some", ""));
}
```