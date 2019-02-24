using System;
using System.Collections.Generic;

namespace Kourin
{
    /// <summary>
    /// スクリプト実行時例外
    /// </summary>
    public class KourinException : Exception
    {
        /// <summary>
        /// スクリプトスタックトレース
        /// </summary>
        public string ScriptStackTrace { get; private set; }

        public KourinException(string message) : this(message, null)
        { }
        public KourinException(string message, Exception ex) : base(message, ex)
        {
            ScriptStackTrace = "";
        }
        internal void AddStackTrace(int line, string block)
        {
            var s = block + " / line:"+line;
            if(ScriptStackTrace != "") s = Environment.NewLine + s;
            ScriptStackTrace += s;
        }
    }

    /// <summary>
    /// スクリプト中断要求例外
    /// </summary>
    public class KourinAbortException : KourinException
    {
        public KourinAbortException(string message) : base(message)
        {
        }
    }
}
