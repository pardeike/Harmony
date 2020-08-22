namespace Execution_With
{
	using System;

	public class Code
	{
		// <example>
		static R ReplacementMethod(T optionalThisArgument /*, ... arguments ... */ )
		{
			R result = default;
			var finalized = false;
			Exception ex = null;

			// All this code is generated dynamically, which means that
			// Harmony can build it depending on
			//
			// - if there are any finalizers (otherwise, skip try-catch)
			//
			// - re-throwing can be dynamic too depending on if at least
			//   one finalizer returns a non-void result

			try
			{
				result = Original(/* ... arguments ... */);

				// finalizers get all the arguments a prefix could get too
				// plus one new one: "Exception __exception"
				// they SHOULD NOT edit the passed exception but instead
				// signal to Harmony that they change it by returning it

				// here finalizers are called without try-catch so they are
				// allowed to throw exceptions. note, that it is perfectly
				// fine to get null passed into the exception argument

				SimpleFinalizer(ref result);
				ex = EditFinalizer(ex, ref result);
				finalized = true;

				if (ex is object) throw ex;
				return result;
			}
			catch (Exception e)
			{
				ex = e;

				// finalizers will get another chance here, so they are
				// guaranteed to run even if their first invocation threw
				// an exception

				if (!finalized)
				{
					try { SimpleFinalizer(ref result); } catch { }
					try { ex = EditFinalizer(ex, ref result); } catch { }
				}

				if (allVoid)
				{
					// alternative 1: all finalizers are returning void
					throw;
				}
				else
				{
					// alternative 2: at least one non-void finalizer
					if (ex is object) throw ex;
				}

				return result;
			}
		}

		// given the following signatures:
		public static R Original() { return new R("original"); }
		public static void SimpleFinalizer(ref R result) { }
		public static Exception EditFinalizer(Exception ex, ref R result) { return ex; }
		// </example>

		public class R { public R(string s) { } }
		public class T { }
		public static bool allVoid;
	}
}
