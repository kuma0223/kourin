using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kourin
{
    /// <summary>
    /// 関数プラグインインタフェース
    /// </summary>
    public interface IPluginFunctionFactory
    {
        /// <summary>
        /// プラグインを判別するキーワードです。
        /// </summary>
        string KeyName { get; }

        /// <summary>
        /// エンジンに登録する追加関数クラスのインスタンスリストを返します。
        /// </summary>
        List<IKourinFunction> GetPluginFunctions();
    }
}
