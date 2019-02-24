using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kourin
{
    /// <summary>
    /// プリセット関数群
    /// </summary>
    static class PresetFunctions
    {
        public static KFunc_EXECUTIONALL executionAll = new KFunc_EXECUTIONALL();
        public static KFunc_CAT    cat = new KFunc_CAT();
        public static KFunc_ADD    add = new KFunc_ADD();
        public static KFunc_SUB    sub = new KFunc_SUB();
        public static KFunc_MUL    mul = new KFunc_MUL();
        public static KFunc_DIV    div = new KFunc_DIV();
        public static KFunc_MOD    mod = new KFunc_MOD();
        public static KFunc_POW    pow = new KFunc_POW();
        public static KFunc_EQUAL  equal = new KFunc_EQUAL();
        public static KFunc_NEQUAL nequal = new KFunc_NEQUAL();
        public static KFunc_OVER   over = new KFunc_OVER();
        public static KFunc_EOVER  eover = new KFunc_EOVER();
        public static KFunc_UNDER  under = new KFunc_UNDER();
        public static KFunc_EUNDER eunder = new KFunc_EUNDER();
        public static KFunc_AND    and = new KFunc_AND();
        public static KFunc_OR     or = new KFunc_OR();
        public static KFunc_NOT    not = new KFunc_NOT();
        public static KFunc_TRUE   true_ = new KFunc_TRUE();
        public static KFunc_FALSE  false_ = new KFunc_FALSE();
        public static KFunc_NULL   null_ = new KFunc_NULL();

        public static KFunc_IF     if_ = new KFunc_IF();
        public static KFunc_REPEAT repeat = new KFunc_REPEAT();
        public static KFunc_WHILE  while_ = new KFunc_WHILE();
        public static KFunc_RETURN return_ = new KFunc_RETURN();
        public static KFunc_TOSCRIPT toscript = new KFunc_TOSCRIPT();

        public static KFunc_DATE   date = new KFunc_DATE();
        public static KFunc_FORMAT format = new KFunc_FORMAT();
        public static KFunc_INT    int_ = new KFunc_INT();
        public static KFunc_DOUBLE double_ = new KFunc_DOUBLE();
        public static KFunc_METHOD method = new KFunc_METHOD();
        public static KFunc_PROPERTY property = new KFunc_PROPERTY();

        public static bool tryNum(object val, out object box)
        {
            if (val == null) { box = null; return false; }
            else if (val is int || val is double) { box = val; return true; }
            else
            {
                var valstr = val.ToString();
                if (valstr.IndexOf(".") > 0)
                {
                    double tmp;
                    bool ret = double.TryParse(valstr, out tmp);
                    box = tmp;
                    return ret;
                }
                else
                {
                    int tmp; bool ret;
                    if(valstr.StartsWith("0x")){
                        ret = int.TryParse(valstr.Substring(2)
                            , System.Globalization.NumberStyles.HexNumber, null, out tmp);
                    }
                    else ret = int.TryParse(valstr, out tmp);
                    box = tmp;
                    return ret;
                }
            }
        }
    }

    /// <summary>
    /// 分割実行式用関数
    /// 渡された引数の一番最後を返す。この関数が呼ばれる前に引数部分は既に実行されている。
    /// </summary>
    class KFunc_EXECUTIONALL : IKourinFunction
    {
        public string name { get { return "EXECUTIONALL"; } }
        
        public object execute(object[] args)
        {
            if (args.Length == 0) throw new KourinException(name + "に式が含まれません。");
            return args.Last();
        }
    }

    /// <summary>
    /// 文字列結合
    /// </summary>
    class KFunc_CAT : IKourinFunction
    {
        public string name { get { return "CAT"; } }
        
        public object execute(object[] args)
        {
            StringBuilder bil = new StringBuilder();
            foreach (var arg in args)
            {
                bil.Append(arg==null?"":arg.ToString());
            }
            return bil.ToString();
        }
    }

    /// <summary>
    /// 演算系スーパークラス
    /// </summary>
    abstract class KFunc_MathBase : IKourinFunction
    {
        public abstract string name { get; }

        public object execute(object[] args)
        {
            if (args.Length != 2)
            {
                throw new KourinException(name + "関数の引数の数が不正です。");
            }

            object arg1; object arg2;
            if (!PresetFunctions.tryNum(args[0], out arg1)) { throw new KourinException("'" + args[0] + "'を数値に変換できません。"); }
            if (!PresetFunctions.tryNum(args[1], out arg2)) { throw new KourinException("'" + args[1] + "'を数値に変換できません。"); }

            if (arg1 is double || arg2 is double)
            {
                Func<object, double> toDouble = new Func<object, double>((i) => { return (i is int ? (double)(int)i : (double)i); });
                return calcDouble(toDouble(arg1), toDouble(arg2));
            }
            else
            {
                return calcInt((int)arg1, (int)arg2);
            }
        }
        public abstract object calcInt(int arg1, int arg2);
        public abstract object calcDouble(double arg1, double arg2);
    }
    class KFunc_ADD : KFunc_MathBase
    {
        public override string name { get { return "ADD"; } }

        public override object calcInt(int arg1, int arg2) { return arg1 + arg2; }
        public override object calcDouble(double arg1, double arg2) { return arg1 + arg2; }

    }
    class KFunc_SUB : KFunc_MathBase
    {
        public override string name { get { return "SUB"; } }

        public override object calcInt(int arg1, int arg2) { return arg1 - arg2; }
        public override object calcDouble(double arg1, double arg2) { return arg1 - arg2; }
    }
    class KFunc_MUL : KFunc_MathBase
    {
        public override string name { get { return "MUL"; } }

        public override object calcInt(int arg1, int arg2) { return arg1 * arg2; }
        public override object calcDouble(double arg1, double arg2) { return arg1 * arg2; }
    }
    class KFunc_DIV : KFunc_MathBase
    {
        public override string name { get { return "DIV"; } }

        public override object calcInt(int arg1, int arg2)
        {
            if (arg2 == 0) { throw new KourinException("0除算が発生しました。"); }
            return arg1 / arg2;
        }
        public override object calcDouble(double arg1, double arg2)
        {
            if (arg2 == 0.0) { throw new KourinException("0除算が発生しました。"); }
            return arg1 / arg2;
        }
    }
    class KFunc_MOD : KFunc_MathBase
    {
        public override string name { get { return "MOD"; } }

        public override object calcInt(int arg1, int arg2)
        {
            if (arg2 == 0) { throw new KourinException("0除算（剰余）が発生しました。"); }
            return arg1 % arg2;
        }
        public override object calcDouble(double arg1, double arg2)
        {
            if (arg2 == 0.0) { throw new KourinException("0除算（剰余）が発生しました。"); }
            return arg1 % arg2;
        }
    }
    class KFunc_POW : KFunc_MathBase
    {
        public override string name { get { return "POW"; } }

        public override object calcInt(int arg1, int arg2) { return Math.Pow(arg1, arg2); }
        public override object calcDouble(double arg1, double arg2) { return Math.Pow(arg1, arg2); }
    }

    /// <summary>
    /// 比較系スーパークラス
    /// </summary>
    abstract class KFunc_CompareBase : IKourinFunction
    {
        public abstract string name { get;}

        public object execute(object[] args)
        {
            if (args.Length != 2){ throw new KourinException(name + "関数の引数の数が不正です。"); }

            try
            {
                object a1; object a2;

                if (args.All(arg => arg is string))
                {
                    return compareString((string)args[0], (string)args[1]);
                }
                if (args.All(arg => arg is bool))
                {
                    return compareBool((bool)args[0], (bool)args[1]);
                }
                else if (PresetFunctions.tryNum(args[0], out a1) && PresetFunctions.tryNum(args[1], out a2))
                {
                    Func<object, double> toDouble = new Func<object, double>((i) => { return (i is int ? (double)(int)i : (double)i); });
                    if (a1 is int && a2 is int) { return compareInt((int)a1, (int)a2); }
                    else { return compareDouble(toDouble(a1), toDouble(a2)); }
                }
                else
                {
                    return compareOther(args[0], args[1]);
                }
            }
            catch (Exception)
            {
                throw new KourinException(name + "で'" + args[0] + "'と'" + args[1] + "'を比較できません。");
            }
            throw new KourinException(name + "で'" + args[0] + "'と'" + args[1] + "'を比較できません。");
        }

        public abstract bool compareInt(int arg1, int arg2);
        public abstract bool compareDouble(double arg1, double arg2);
        public abstract bool compareString(string arg1, string arg2);
        public abstract bool compareBool(bool arg1, bool arg2);
        public abstract bool compareOther(object arg1, object arg2);
    }
    class KFunc_OVER : KFunc_CompareBase
    {
        public override string name { get { return "OVER"; } }
        public override bool compareInt(int arg1, int arg2) { return arg1 > arg2; }
        public override bool compareDouble(double arg1, double arg2) { return arg1 > arg2; }
        public override bool compareString(string arg1, string arg2) { throw new Exception(); }
        public override bool compareBool(bool arg1, bool arg2) { throw new Exception(); }
        public override bool compareOther(object arg1, object arg2){ throw new Exception(); }
    }
    class KFunc_EOVER : KFunc_CompareBase
    {
        public override string name { get { return "EOVER"; } }
        public override bool compareInt(int arg1, int arg2) { return arg1 >= arg2; }
        public override bool compareDouble(double arg1, double arg2) { return arg1 >= arg2; }
        public override bool compareString(string arg1, string arg2) { throw new Exception(); }
        public override bool compareBool(bool arg1, bool arg2) { throw new Exception(); }
        public override bool compareOther(object arg1, object arg2){ throw new Exception(); }
    }
    class KFunc_UNDER : KFunc_CompareBase
    {
        public override string name { get { return "UNDER"; } }
        public override bool compareInt(int arg1, int arg2) { return arg1 < arg2; }
        public override bool compareDouble(double arg1, double arg2) { return arg1 < arg2; }
        public override bool compareString(string arg1, string arg2) { throw new Exception(); }
        public override bool compareBool(bool arg1, bool arg2) { throw new Exception(); }
        public override bool compareOther(object arg1, object arg2){ throw new Exception(); }
    }
    class KFunc_EUNDER : KFunc_CompareBase
    {
        public override string name { get { return "EUNDER"; } }
        public override bool compareInt(int arg1, int arg2) { return arg1 <= arg2; }
        public override bool compareDouble(double arg1, double arg2) { return arg1 <= arg2; }
        public override bool compareString(string arg1, string arg2) { throw new Exception(); }
        public override bool compareBool(bool arg1, bool arg2) { throw new Exception(); }
        public override bool compareOther(object arg1, object arg2){ throw new Exception(); }
    }
    class KFunc_EQUAL : KFunc_CompareBase
    {
        public override string name { get { return "EQUAL"; } }
        public override bool compareInt(int arg1, int arg2) { return arg1 == arg2; }
        public override bool compareDouble(double arg1, double arg2) { return arg1 == arg2; }
        public override bool compareString(string arg1, string arg2) { return arg1 == arg2; }
        public override bool compareBool(bool arg1, bool arg2) { return arg1 == arg2; }
        public override bool compareOther(object arg1, object arg2){ return arg1 == arg2; }
    }
    class KFunc_NEQUAL : KFunc_CompareBase
    {
        public override string name { get { return "NEQUAL"; } }
        public override bool compareInt(int arg1, int arg2) { return arg1 != arg2; }
        public override bool compareDouble(double arg1, double arg2) { return arg1 != arg2; }
        public override bool compareString(string arg1, string arg2) { return arg1 != arg2; }
        public override bool compareBool(bool arg1, bool arg2) { return arg1 != arg2; }
        public override bool compareOther(object arg1, object arg2){ return arg1 != arg2; }
    }

    /// <summary>
    /// 論理演算系スーパークラス
    /// </summary>
    abstract class KFunc_AndOrBase : IKourinFunction
    {
        public abstract string name { get; }

        public object execute(object[] args)
        {
            if (args.Length != 2) { throw new KourinException(name + "関数の引数の数が不正です。"); }
            if (!(args[0] is bool)) { throw new KourinException(name + "関数で'" + args[0] + "'を評価できません。"); }
            if (!(args[1] is bool)) { throw new KourinException(name + "関数で'" + args[1] + "'を評価できません。"); }

            return compare((bool)args[0], (bool)args[1]);
        }
        public abstract bool compare(bool arg1, bool arg2);
    }
    class KFunc_AND : KFunc_AndOrBase
    {
        public override string name { get { return "AND"; } }
        public override bool compare(bool arg1, bool arg2) { return arg1 && arg2; }
    }
    class KFunc_OR : KFunc_AndOrBase
    {
        public override string name { get { return "OR"; } }
        public override bool compare(bool arg1, bool arg2) { return arg1 || arg2; }
    }
    class KFunc_NOT : IKourinFunction
    {
        public string name { get { return "NOT"; } }

        public object execute(object[] args)
        {
            if (args.Length != 1) { throw new KourinException(name + "関数の引数の数が不正です。"); }
            if (!(args[0] is bool)) { throw new KourinException(name + "関数で'" + args[0] + "'を評価できません。"); }
            return !(bool)args[0];
        }
    }

    
    /// <summary>
    /// trueリテラル
    /// </summary>
    class KFunc_TRUE : IKourinFunction
    {
        public string name { get { return "TRUE"; } }
        public object execute(object[] args) { return true; }
    }
    /// <summary>
    /// falseリテラル
    /// </summary>
    class KFunc_FALSE : IKourinFunction
    {
        public string name { get { return "FALSE"; } }
        public object execute(object[] args) { return false; }
    }
    /// <summary>
    /// nullリテラル
    /// </summary>
    class KFunc_NULL : IKourinFunction
    {
        public string name { get { return "NULL"; } }
        public object execute(object[] args) { return null; }
    }


    /// <summary>
    /// IF関数（特別関数：実装はEngine参照）
    /// </summary>
    class KFunc_IF : IKourinFunction
    {
        public string name { get { return "IF"; } }

        public object execute(object[] args)
        {
            if (args.Length < 2)
            {
                throw new KourinException(name + "関数の引数の数が不正です。");
            }
            if (!(args[0] is bool))
            {
                throw new KourinException(name + "関数で'" + args[0] + "'を評価できません。");
            }
            if ((bool)args[0]) { return args[1]; }
            else { return args.Length<3 ? "NULL" : args[2]; }
        }
    }

    /// <summary>
    /// REPEAT関数（特別関数：実装はEngine参照）
    /// </summary>
    class KFunc_REPEAT : IKourinFunction
    {
        public string name { get { return "REPEAT"; } }
        
        public object execute(object[] args)
        {
            if (args.Length != 2)
            {
                throw new KourinException(name + "関数の引数の数が不正です。");
            }
            if (!(args[0] is int))
            {
                throw new KourinException(name + "関数で'" + args[0] + "'を評価できません。");
            }
            return null;
        }
    }

    /// <summary>
    /// WHILE関数（特別関数：実装はEngine参照）
    /// </summary>
    class KFunc_WHILE : IKourinFunction
    {
        public string name { get { return "WHILE"; } }
        
        public object execute(object[] args)
        {
            if (args.Length != 2)
            {
                throw new KourinException(name + "関数の引数の数が不正です。");
            }
            return null;
        }
    }

    /// <summary>
    /// RETRUN関数
    /// </summary>
    class KFunc_RETURN : IKourinFunction
    {
        public string name { get { return "RETURN"; } }        
        public object execute(object[] args)
        {
            if(args.Length ==0) return new ReturnedObject(null);
            else {
                if(args[0] is ReturnedObject) throw new KourinException(name + "関数の引数にReturnedObjectを指定できません。");
                return new ReturnedObject(args[0]);
            }
        }
    }
    //リターン用クラス
    internal class ReturnedObject
    {
        public object value;
        internal ReturnedObject(object value){
            this.value = value;
        }
    }

    /// <summary>
    /// スクリプト化（特別関数：実装はEngine参照）
    /// </summary>
    class KFunc_TOSCRIPT : IKourinFunction
    {   //関数名チェック用ダミー
        public string name { get { return "TOSCRIPT"; } }
        public object execute(object[] args)
        { return null; }
    }

    /// <summary>
    /// 日時取得
    /// </summary>
    class KFunc_DATE : IKourinFunction
    {
        public string name { get { return "DATE"; } }

        public object execute(object[] args)
        {
            if (args.Length < 1) {
                return DateTime.Now;
            }
            else {
                DateTime d;
                if (args[0] is string && DateTime.TryParse((string)args[0],out d))
                {
                    return d;
                }
            }
            throw new KourinException("'" + args[0] + "'を日時へ変更できません。");
        }
    }

    /// <summary>
    /// 文字列フォーマット
    /// </summary>
    class KFunc_FORMAT : IKourinFunction
    {
        public string name { get { return "FORMAT"; } }

        public object execute(object[] args)
        {
            if (args.Length < 1)
            {
                throw new KourinException(name + "関数の引数の数が不正です。");
            }
            if(!(args[0] is string))
            {
                throw new KourinException(name + "関数の第一引数が文字列ではありません。");
            }

            var objs = new object[args.Length - 1];
            for (int i = 1; i < args.Length; i++) { objs[i - 1] = args[i]; }
            try { 
                return String.Format((string)args[0], objs);
            }catch(FormatException ex)
            {
                throw new KourinException(ex.Message);
            }
        }
    }

    /// <summary>
    /// 整数変換
    /// </summary>
    class KFunc_INT : IKourinFunction
    {
        public string name { get { return "INT"; } }

        public object execute(object[] args)
        {
            if (args.Length < 1){ return 0; }
            try
            {
                object obj = args[0];
                if(obj is string) {
                    var s = (string)args[0];
                    if (s.IndexOf("0x") >= 0){ return Convert.ToInt32(s, 16); }
                    else { return Convert.ToInt32(s); }
                }
                else if (obj is double) { return (int)(double)obj; }
                else if (obj is float)  { return (int)(float)obj; }
                else if (obj is long)   { return (int)(long)obj; }
                return Convert.ToInt32(args[0]);
            }
            catch(FormatException)
            {
                throw new KourinException("'" + args[0] + "'を整数へ変換できません。");
            }
        }
    }
    
    /// <summary>
    /// 実数変換
    /// </summary>
    class KFunc_DOUBLE : IKourinFunction
    {
        public string name { get { return "DOUBLE"; } }

        public object execute(object[] args)
        {
            if (args.Length < 1){ return 0.0; }
            try
            {
                object obj = args[0];
                if(obj is string) return Convert.ToDouble((string)obj);
                return Convert.ToDouble(args[0]);
            }
            catch(FormatException)
            {
                throw new KourinException("'" + args[0] + "'を実数へ変換できません。");
            }
        }
    }

    /// <summary>
    /// クラスメソッド呼び出し
    /// </summary>
    class KFunc_METHOD : IKourinFunction
    {
        public string name { get { return "METHOD"; } }
        
        public object execute(object[] args)
        {
            if(args.Length < 2) throw new KourinException(this.name + "関数の引数が不足しています。");

            var cls = args[0].GetType();
            var name = "" + args[1];

            System.Reflection.MethodInfo method = null;
            object[] param = null;

            if(args.Length > 2){
                param = args.Where((obj, idx)=>{ return idx>=2; }).ToArray();
                Type[] types = (from x in param select x.GetType()).ToArray();
                method = cls.GetMethod(name, types);
                if(method == null) method = cls.GetMethod(name);
            }
            else
            {
                method = cls.GetMethod(name, new Type[0]);
            }

            if(method == null) throw new KourinException("メソッド'" + name + "'が見つかりません。");
            return method.Invoke(args[0], param);
        }
    }
    
    /// <summary>
    /// クラスプロパティ呼び出し
    /// </summary>
    class KFunc_PROPERTY : IKourinFunction
    {
        public string name { get { return "PROPERTY"; } }
        
        public object execute(object[] args)
        {
            if(args.Length < 2) throw new KourinException(this.name + "関数の引数が不足しています。");
            
            var cls = args[0].GetType();
            var name = "" + args[1];
            
            var prop = cls.GetProperty(name);
            if(prop == null) throw new KourinException("プロパティ'" + name + "'が見つかりません。");

            if(args.Length > 2){
                prop.SetValue(args[0], args[2]);
                return null;
            }
            else{
                return prop.GetValue(args[0]);
            }
        }
    }
}
