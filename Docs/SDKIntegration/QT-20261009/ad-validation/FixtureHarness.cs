// Standalone state-machine test harness. Not part of Assets or any player build.
using System;
namespace NutSort.Content
{
    public sealed class OriginalUserDefaults { }
    public sealed class OriginalUserLocalData
    {
        public int NormalGetTimes, InterAdTimes, LuckyScrewDoneCount, Level, GuideIndex;
        public long Level1TXTime, Level2TXTime;
        public string TXTargetGold;
        public OriginalUserLocalData(OriginalUserDefaults defaults) { }
    }
}
namespace UnityEngine
{
    public static class Resources { public static T Load<T>(string path) where T : new() => new T(); }
    public static class Debug { public static void Log(object value) => Console.WriteLine(value); }
}
public static class FixtureHarness
{
    public static int Main()
    {
        try { NutSort.Validation.OriginalRewardGetFlowValidation.Validate(); return 0; }
        catch(Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
