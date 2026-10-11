using HarmonyLib;
using NUnit.Framework;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Text;

namespace HarmonyLibTests.Tools
{
	[TestFixture, NonParallelizable]
	public class Test_HarmonyDebugLog : TestLogger
	{
		[TestCase("en-US")]
		[TestCase("th-TH")]
		[TestCase("ar-SA")]
		[TestCase("fa-IR")]
		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Test_ConstructorTimestampUsesInvariantCalendarAnd24HourClock(string cultureName)
		{
			var previousCulture = Thread.CurrentThread.CurrentCulture;
			var previousWriter = FileLog.LogWriter;
			var previousDebug = Harmony.DEBUG;
			var previousEnvironment = Environment.GetEnvironmentVariable("HARMONY_DEBUG");
			using var output = new MemoryStream();
			using var writer = new StreamWriter(output) { AutoFlush = true };
			try
			{
				Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
				Environment.SetEnvironmentVariable("HARMONY_DEBUG", "1");
				FileLog.LogWriter = writer;
				var before = DateTime.Now;
				_ = new Harmony("test.debug.timestamp");
				var after = DateTime.Now;
				var line = Encoding.UTF8.GetString(output.ToArray()).Split((char)10).Select(value => value.Trim()).Single(value => value.StartsWith("### At ", StringComparison.Ordinal));
				var timestamp = line.Substring("### At ".Length);
				Assert.AreEqual(19, timestamp.Length);
				Assert.True(DateTime.TryParseExact(timestamp, "yyyy-MM-dd HH.mm.ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var logged));
				var earliest = before.AddTicks(-(before.Ticks % TimeSpan.TicksPerSecond));
				Assert.That(logged, Is.InRange(earliest, after));
			}
			finally
			{
				Thread.CurrentThread.CurrentCulture = previousCulture;
				FileLog.LogWriter = previousWriter;
				Harmony.DEBUG = previousDebug;
				Environment.SetEnvironmentVariable("HARMONY_DEBUG", previousEnvironment);
			}
		}
	}
}
