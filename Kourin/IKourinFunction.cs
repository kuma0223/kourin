using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kourin
{
    /// <summary>
    /// スクリプト関数インタフェース
    /// </summary>
    public interface IKourinFunction
    {
        /// <summary>
        /// 関数名を取得します。
        /// </summary>
        string name { get; }
        
        /// <summary>
        /// 関数を呼び出し結果を返します。
        /// </summary>
        /// <param name="args">引数</param>
        /// <returns>結果値</returns>
        object execute(object[] args);
    }
    
    /// <summary>
    /// 簡易関数実装用クラス
    /// 関数名と実装式を指定してIKourinFunctionを実装したインスタンスを生成します。
    /// </summary>
    public class KourinFunction : IKourinFunction
    {
        /// <summary>関数名</summary>
        public string name { private set; get; }
        private Func<object[], object> executeMethod;

        /// <summary>
        /// 関数名と実装式を指定してインスタンスを生成します。
        /// </summary>
        /// <param name="name">関数名称</param>
        /// <param name="executeMethod">関数実装(入力, 出力)</param>
        public KourinFunction(string name, Func<object[], object> executeMethod )
        {
            this.name = name;
            this.executeMethod = executeMethod;
        }

        /// <summary>
        /// 関数実行
        /// </summary>
        public object execute(object[] args)
        {
            return executeMethod(args);
        }
    }

    /// <summary>
    /// スクリプト内定義関数用クラス
    /// </summary>
    internal class ScriptFunction : IKourinFunction
    {
        public string name { private set; get; }
        public string script { private set; get; }

        public ScriptFunction(string name, string script)
        {
            this.name = name; this.script = script;
        }
        public object execute(object[] args)
        {
            throw new KourinException("Mostn't call script function direct");
        }
    }
}
