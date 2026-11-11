namespace FP2.Captions
{
    public static class VoicePresets
    {
        public static readonly string[] EnglishNames = { "US English", "UK English", "Australian English" };
        public static readonly string[] MandarinNames = { "Mainland Mandarin", "Taiwan Mandarin" };
        public static string Name(string language, int index)
        {
            var names = language == "zh" ? MandarinNames : EnglishNames;
            return names[Normalize(index, names.Length)];
        }
        public static string Id(string language, int index)
        {
            if (language == "zh")
                return Normalize(index, 2) == 0 ? "zh-CN-XiaoxiaoNeural" : "zh-TW-HsiaoChenNeural";
            return new[] { "alloy", "en-GB-SoniaNeural", "en-AU-NatashaNeural" }[Normalize(index, 3)];
        }
        public static int Count(string language) => language == "zh" ? MandarinNames.Length : EnglishNames.Length;
        private static int Normalize(int index, int count) => ((index % count) + count) % count;
    }
}
