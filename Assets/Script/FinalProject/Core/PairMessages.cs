using System;
namespace FP2.Captions
{
    public static class PairMessages
    {
        public static string Waiting(string role, string phase, bool chinese)
        {
            if (role != "A" && role != "B") role = "?";
            if (phase == "processing") return chinese ? role + " 正在翻译，请稍等。" : role + " is translating. Please wait.";
            if (phase == "reviewing") return chinese ? role + " 正在确认译文，请稍等。" : role + " is reviewing the translation. Please wait.";
            return chinese ? role + " 正在说话，请稍等。" : role + " is speaking. Please wait.";
        }
        public static string Offline(bool chinese) => chinese
            ? "双人会话未连接，录音已暂停。请检查局域网中转服务。"
            : "Pair session disconnected. Recording paused. Check the LAN relay.";
        public static string Ready(bool chinese) => chinese
            ? "双人会话已连接，按住右扳机发言。" : "Pair connected. Hold RIGHT trigger to speak.";
    }
}
