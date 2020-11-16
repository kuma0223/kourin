using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Text.RegularExpressions;

namespace Kourin
{
    /// <summary>
    /// スクリプトエンジン
    /// </summary>
    public class KourinEngine
    {
        /// <summary>
        /// スクリプト実行中か否か
        /// </summary>
        public bool isRunning { private set; get; }

        /// <summary>
        /// 関数テーブル
        /// </summary>
        private Dictionary<string, IKourinFunction> functionTable = new Dictionary<string, IKourinFunction>(StringComparer.OrdinalIgnoreCase);
        
        /// <summary>
        /// 変数テーブル
        /// </summary>
        public Dictionary<string, object> variables = new Dictionary<string, object>();

        /// <summary>
        /// 変数テーブル（スコープ）
        /// </summary>
        private Dictionary<string, object> svariables = new Dictionary<string, object>();

        /// <summary>
        /// 停止要求フラグ
        /// </summary>
        private bool beStop = false;

        /// <summary>
        /// 登録済み関数一覧
        /// </summary>
        public string[] functions
        {
            get { return functionTable.Keys.ToArray(); }
        }

        /// <summary>
        /// 同時にひとつのスクリプトを実行できるインスタンスを生成します。
        /// </summary>
        public KourinEngine()
        {
            setFunction(PresetFunctions.executionAll);
            setFunction(PresetFunctions.cat);
            setFunction(PresetFunctions.add);
            setFunction(PresetFunctions.sub);
            setFunction(PresetFunctions.mul);
            setFunction(PresetFunctions.div);
            setFunction(PresetFunctions.mod);
            setFunction(PresetFunctions.pow);
            setFunction(PresetFunctions.equal);
            setFunction(PresetFunctions.nequal);
            setFunction(PresetFunctions.over);
            setFunction(PresetFunctions.eover);
            setFunction(PresetFunctions.under);
            setFunction(PresetFunctions.eunder);
            setFunction(PresetFunctions.and);
            setFunction(PresetFunctions.or);
            setFunction(PresetFunctions.true_);
            setFunction(PresetFunctions.false_);
            setFunction(PresetFunctions.null_);
            setFunction(PresetFunctions.not);
            setFunction(PresetFunctions.if_);
            setFunction(PresetFunctions.repeat);
            setFunction(PresetFunctions.while_);
            setFunction(PresetFunctions.return_);
            setFunction(PresetFunctions.toscript);
            setFunction(PresetFunctions.date);
            setFunction(PresetFunctions.format);
            setFunction(PresetFunctions.int_);
            setFunction(PresetFunctions.double_);
            setFunction(PresetFunctions.method);
            setFunction(PresetFunctions.property);
            //文字列をスクリプトとして実行する関数
            setFunction(new KourinFunction("DOSCRIPT", (args)=>{
                if(args.Length==0) throw new KourinException("DOSCRIPT関数の引数が不足しています。");
                return this.rideScript(args[0].ToString(), "DOSCRIPT");
            }));
        }

        /// <summary>
        /// 拡張関数を設定します。
        /// </summary>
        public void setFunction(IKourinFunction function)
        {
            functionTable[function.name] = function;
        }
        /// <summary>
        /// 関数を削除します。
        /// </summary>
        /// <param name="name">関数名</param>
        public void releaseFunction(string name)
        {
            if(functionTable.ContainsKey(name))
                functionTable.Remove(name);
        }

        /// <summary>
        /// DLLのパスを指定してプラグイン関数を読み込みます。
        /// keyを指定すると該当するKeyNameを持つプラグインのみを読み込みます。
        /// </summary>
        /// <param name="path">DLLパス</param>
        /// <param name="key">シリーズキー名</param>
        public void loadPluginDll(string path, string key = null)
        {
            var asm = System.Reflection.Assembly.LoadFrom(path);
            foreach (var t in asm.GetTypes())
            {
                if(t.IsAbstract || t.IsInterface) continue;
                if(!t.GetInterfaces().Contains(typeof(IPluginFunctionFactory))) continue;
                var fac = System.Activator.CreateInstance(t) as IPluginFunctionFactory;
                if(fac != null && (key==null || fac.KeyName==key)){
                    fac.GetPluginFunctions().ForEach(x => setFunction(x));
                }
            }
        }

        /// <summary>
        /// スクリプトを実行します。
        /// </summary>
        /// <param name="script">スクリプト文字列</param>
        /// <returns>結果値</returns>
        public object execute(string script)
        {
            using (var reader = new System.IO.StringReader(script))
                return execute(reader);
        }
        /// <summary>
        /// スクリプトを実行します。
        /// </summary>
        /// <param name="reader">スクリプト文字列取得リーダ</param>
        /// <returns>結果値</returns>
        public object execute(System.IO.TextReader reader)
        {
            if(isRunning) throw new KourinException("スクリプトが既に実行中です。");
            try {
                beStop = false;
                isRunning = true;
                var ret = rideScript(reader, "Script root");
                if(ret is ReturnedObject) ret=((ReturnedObject)ret).value; //RETRUN復元
                return ret;
            }finally {
                svariables.Clear();
                isRunning = false;
            }
        }

        /// <summary>
        /// スクリプトを停止します。
        /// このメソッドを呼んだ時点で実行されている処理ブロックが
        /// 完了した時点でKourinAbortExeptionが発生します。
        /// スクリプトが既に停止している場合、何も起りません。
        /// </summary>
        public void stop()
        {
            beStop = true;
        }

        //◆━━━━━━━━━━━━━━━━━━━━━━━━━━━━◆
        
        private object rideScript(string script, string blockName)
        {
            using (var reader = new System.IO.StringReader(script))
                return rideScript(reader, blockName);
        }
        private object rideScript(System.IO.TextReader reader, string blockName)
        {  
            object ret = null;
            int line = 0;
            int bcount = 0; //{}のカウント
            StringBuilder buf = new StringBuilder(128);

            try {
                string str;
                while((str = reader.ReadLine()) != null) {
                    line++;

                    LINESTART:

                    //コメントと前後空白の削除
                    //スタックトレースのため行が空でも削除してはいけない
                    bool isHereDoc;
                    str = fairScriptText(str, out isHereDoc);
                    
                    //整形済みテキスト開始行の場合は終了行までを含める
                    if (isHereDoc) {
                        buf.Append(str);
                        buf.Append('\n');

                        var sb = new StringBuilder(128);
                        int co = 0;

                        while (true) {
                            str = reader.ReadLine();
                            line++;

                            if (str == null) { 
                                throw new KourinException("ヒア文字列が閉じられていません。");
                            }
                            if (str.StartsWith($"{StrChar}$")) {
                                buf.Append(sb);
                                buf.Append($"\n{StrChar}$");
                                str = str.Substring(2);
                                goto LINESTART;
                            }

                            if (co > 0) sb.AppendLine();
                            sb.Append(str);
                            co++;
                        }
                    }

                    //行末が{の場合、行頭が}の行までを実行単位に含める
                    if(str.Length>0 && str.Last() == '{') bcount++;
                    if(str.Length>0 && str.First() == '}') bcount--;

                    if(bcount != 0) {
                        buf.Append(str);
                        buf.Append('\n');
                        continue;
                    }

                    //行末が_の場合は次行へ継続
                    if (str.Length > 0 && str.Last() == '_') {
                        buf.Append(str.Substring(0, str.Length - 1));
                        continue;
                    }

                    //スクリプト実行
                    buf.Append(str);

                    int tokencount;
                    var tmp = rideOne(buf.ToString(), out tokencount);
                    if (tokencount > 0) ret = tmp; //空行の戻り値は無視
                    if (ret is ReturnedObject) break; //実行結果がRETURNの場合その行で抜ける
                    buf.Clear();
                }
                if(bcount!=0) throw new KourinException("}が不足しています。");
            } catch(KourinException ex) {
                ex.AddStackTrace(line, blockName);
                throw;
            } catch (Exception ex) {
                var ke = new KourinException(ex.Message, ex);
                ke.AddStackTrace(line, blockName);
                throw ke;
            }
            return ret;
        }
        
        /// <summary>
        /// 単位実行
        /// </summary>
        private object rideOne(string script)
        {
            int i;
            return rideOne(script, out i);
        }
        private object rideOne(String script, out int tokenCount)
        {
            tokenCount = 0;
            if(beStop) throw new KourinAbortException("停止要求によりスクリプトを中断しました。");
            if (script.Length==0) { return null; }
            
            //字句解析
            //checkError(script); トークン分割前のチェックは処理が分散するので辞め
            var tokens = splitScript(script);
            if (tokens.Count == 0) { return null; }
            tokenCount = tokens.Count;
            checkError(tokens);

            //逆ポーランド変換（単項目の場合は無駄だからやらない）
            if (tokens.Count > 1) { tokens = toRPN(tokens); }

            //走行
            return runTokens(tokens);
        }

        //◆━━━━━━━━━━━━━━━━━━━━━━━━━━━━◆
        //字句解析セクション
        
        //文字列リテラル開始終了文字
        private const char StrChar = '"';
        //エスケープ文字
        private const char EscChar = '\\';
        //エスケープ対象
        private static Dictionary<char, char> EscTable = new Dictionary<char, char>(){
            { StrChar, StrChar }, { EscChar, EscChar }, { 'n', '\n'}, { 'r', '\r'}
        };

        /// <summary>
        /// コメントとスペースを削除整理する
        /// </summary>
        private string fairScriptText(string script, out bool inHereDoc)
        {
            inHereDoc = false;

            StringBuilder buf = new StringBuilder(script.Length);
            int i=0;
            int len=script.Length;

            //行頭末スペース削除
            for(;i<script.Length && (script[i]==' ' || script[i]=='\t'); i++);
            for(;len>0 && (script[len-1]==' ' || script[len-1]=='\t'); len--);
            if(i>=len) return "";

            bool isstr = false;
            for(; i<len; i++) {
                var c = script[i];
                var cnext = i<len-1 ? script[i+1] : '\0';
                var cnext2 = i<len-2 ? script[i+2] : '\0';
                var lastadd = buf.Length>0 ? buf[buf.Length-1] : '\0';

                if(isstr) {
                    buf.Append(c);
                    if(isStrEndChar(script, i)){
                        isstr=false;
                    }
                } else {
                    if(c=='/' && cnext=='/') {
                        break; //行コメント
                    }
                    else if(c=='\t' || c==' ') {
                        //タブ文字もスペース。連続空白は1つに。
                        if(lastadd!=' ') buf.Append(' ');
                    }
                    else if(c==StrChar) {
                        buf.Append(c);
                        isstr=true;
                        inHereDoc = false;
                    }
                    else if(c == '$' && cnext == StrChar) {
                        //ヒア文字列の開始
                        buf.Append(c);
                        buf.Append(cnext);
                        i++;
                        inHereDoc = true;
                    }
                    else {
                        buf.Append(c);
                        inHereDoc = false;
                    }
                }
            }
            //行末スペース削除（コメント削除によりでた可能性）
            for(i=0; i<buf.Length && buf[buf.Length-i-1]==' '; i++);
            buf.Remove(buf.Length-i, i);

            return buf.ToString();
        }

        /// <summary>
        /// 字句解析
        /// </summary>
        private List<Token> splitScript(string script)
        {
            Func<char, bool> isNum = (c) => {
                return '0' <= c && c <= '9';
            };
            Func<int, char> charAt = (idx) => {
                return idx<0 || idx>=script.Length ? '\0' : script[idx];
            };

            List<Token> ret = new List<Token>();
            int i = 0;
            while (i < script.Length)
            {
                var c = script[i];
                if (c == ' ') {
                    i++; continue;
                }
                //--------------------成形済み文字列
                else if (c == '$' && charAt(i+1) == StrChar && charAt(i+2) == '\n')
                {
                    StringBuilder buf = new StringBuilder(128);
                    i += 3;
                    while (true) {
                        var cc = charAt(i);
                        var next1 = charAt(i+1);
                        var next2 = charAt(i+2);

                        if (cc == '\0') {
                            throw new KourinException("ヒア文字列が閉じられていません。");
                        } else if(cc == '\n' && next1 == StrChar && next2 == '$') {
                            i+=3;
                            break;
                        } else {
                            buf.Append(cc);
                            i++;
                        }
                    }
                    ret.Add(new Token(TokenType.STR, buf.ToString()));
                }
                //--------------------文字列
                else if (c == StrChar)
                {
                    StringBuilder buf = new StringBuilder(16);
                    i++;
                    while(true){
                        var cc = charAt(i);
                        i++;

                        if (cc == '\0') {
                            throw new KourinException("文字列が閉じられていません。");
                        } else if (cc==StrChar) {
                            //終了判定(エスケープは飛ばしてるので考慮不要)
                            break;
                        } else if (cc == EscChar) {
                            //エスケープ処理
                            var next = charAt(i);
                            i++;
                            if (EscTable.ContainsKey(next)) {
                                buf.Append(EscTable[next]);
                            } else {
                                throw new KourinException($"不明なエスケープ文字'{EscChar+next}'です。");
                            }
                        } else {
                            buf.Append(cc);
                        }
                    }
                    ret.Add(new Token(TokenType.STR, buf.ToString()));
                }
                //--------------------数値（リテラル）
                else if (isNum(c) || (c == '-' && isNum(charAt(i+1))
                    && (ret.Count == 0 || ret.Last().type==TokenType.LPAR || ret.Last().type==TokenType.OPE)))
                {
                    int stIndex = i;
                    if (c == '-') { i++; }

                    for(var cc = charAt(i);
                        cc!='\0' && cc!=' ' && !Operator.isOerator(cc) && cc != ')' && cc != ':';
                        i++, cc = charAt(i));

                    ret.Add(new Token(TokenType.NUM, script.Substring(stIndex, i - stIndex)));
                }
                //--------------------関数 OR 関数宣言
                else if (c == '[' || c == '{' || ('a' <= c && c <= 'z') || ('A' <= c && c <= 'Z') || c==':')
                {
                    bool isPipe = false;
                    if(c == ':')
                    {   //パイプ
                        i++;
                        while(charAt(i) == ' ') i++;
                        isPipe = true;
                    }

                    int stindex = i;

                    Func<char, bool> isEnableChar = x => {
                        if (x == ' ') return false;
                        if (x == '[') return false;
                        if (x == '{') return false;
                        if (x == ':') return false;
                        if (x == '\0') return false;
                        if (x == StrChar) return false;
                        if (Operator.isOerator(x)) return false;
                        return true;
                    };
                    while (isEnableChar(charAt(i))) { i++; }
                    var funcName = script.Substring(stindex, i - stindex);

                    //[{の前の空白は許す
                    while (charAt(i) == ' '){ i++; };
                    
                    if (charAt(i)=='\0' || script[i] != '[' && script[i] != '{')
                    {   //名称のみ=引数なし関数呼び出し
                        ret.Add(new FunctionToken(funcName, funcName, isPipe));
                    }
                    else if(script[i] == '[')
                    {   //関数呼び出し文
                        string[] args;
                        i = skipBlock(script, i, '[', ']', ',', out args);
                        var text = script.Substring(stindex, i-stindex);
                        var token = new FunctionToken(funcName, text, isPipe);
                        token.args.AddRange(args);
                        ret.Add(token);
                    }
                    else if(script[i] == '{')
                    {   //関数作成文
                        if(isPipe) throw new Exception("関数宣言にパイプを繋げることはできません。");
                        string[] box;
                        i = skipBlock(script, i, '{', '}', -1, out box);
                        var text = script.Substring(stindex, i-stindex);
                        var token = new MakeFunctionToken(funcName, box.Any() ? box[0] : "", text);
                        ret.Add(token);
                    }
                }
                //--------------------演算子
                else if (Operator.isOerator(c))
                {
                    string tmpstr;
                    if (Operator.isOerator(charAt(i + 1)))
                    {   //== != >= <= && || など
                        tmpstr = script.Substring(i, 2);
                        i += 2;
                    }else{
                        tmpstr = char.ToString(c);
                        i++;
                    }
                    var ope = Operator.parse(tmpstr);
                    if(ope == null) throw new KourinException("不明な演算子'"+tmpstr+"'です。");
                    ret.Add(new OperatorToken(ope, tmpstr));
                }
                //--------------------(括弧
                else if (c == '(')
                {
                    ret.Add(new Token(TokenType.LPAR, char.ToString(c)));
                    i++;
                }
                //--------------------)括弧
                else if (c == ')')
                {
                    ret.Add(new Token(TokenType.RPAR, char.ToString(c)));
                    i++;
                }
                //--------------------変数
                else if (c == '$')
                {
                    int stindex = i;

                    var dcount = 0;
                    while(charAt(i)=='$') { i++; dcount++; }

                    Func<char, bool> isEnableChar = x => {
                        if (x == ' ') return false;
                        if (x == '(') return false;
                        if (x == ')') return false;
                        if (x == ':') return false;
                        if (x == '\0') return false;
                        if (x == StrChar) return false;
                        if (Operator.isOerator(x)) return false;
                        return true;
                    };
                    
                    while(isEnableChar(charAt(i))) i++;

                    var text = script.Substring(stindex, i - stindex);
                    var name = text.Substring(dcount, text.Length- dcount);
                    var token = new VariableToken(name, text);

                    if (token.varName == "") throw new KourinException("$の後に変数名がありません。");
                    if (dcount > 2) throw new KourinException($"不正な$の連続'{text}'です。");

                    token.isGlobal = dcount == 2;
                    token.isScoped = dcount == 1;

                    ret.Add(token);
                }
                else
                {
                    throw new KourinException("不明なトークン'" + char.ToString(c) + "'です。");
                }
            }

            //末尾の;演算子は無視する。
            if(ret.Count>0 && ret.Last().type==TokenType.OPE && ret.Last().text==";")
                ret.RemoveAt(ret.Count-1);

            return ret;
        }

        /*
        /// <summary>
        /// 字句解析前チェック
        /// </summary>
        private void checkError(string script)
        {
            int dqCount = 0; //"カウント
            Stack<char> pbox = new Stack<char>(); //括弧バッファ
            var pairs = new Dictionary<char, char>{ //括弧組合わせ
                {')', '('}, { ']', '['}, {'}', '{'}
            };

            for (int i = 0; i < script.Length; i++)
            {
                var c = script[i];
                if (dqCount % 2 == 0){
                    if (c == StrChar) {
                        dqCount++;
                    }else if (pairs.ContainsValue(c)){
                        pbox.Push(c);
                    }else if(pairs.ContainsKey(c)){
                        if (pbox.Count==0 || pbox.Pop()!=pairs[c])
                            throw new KourinException("括弧()[]の対応がとれていません。");
                    }
                } else { //文字列内部
                    if(isStrEndChar(script, i)) dqCount--;
                }
            }
            if (dqCount % 2 != 0) { throw new KourinException("\"の対応が取れていません。"); }
            if (pbox.Count > 0) {
                throw new KourinException("括弧()[]{}の対応がとれていません。");
            }
        }
        */

        /// <summary>
        /// 字句解析後チェック
        /// </summary>
        private void checkError(List<Token> tokens)
        {
            var isOperand = new Func<Token,bool>((t)=>
            {
                return t.type == TokenType.STR
                    || t.type == TokenType.NUM
                    || t.type == TokenType.FUNC
                    || t.type == TokenType.VAR
                    || t.type == TokenType.MKFUNC;
            });

            int perCount = 0;

            for (var i = 0; i < tokens.Count; i++ )
            {
                var t = tokens[i];
                if (t.type == TokenType.OPE)
                {
                    if (i == 0 || i == tokens.Count - 1) { throw new KourinException("演算子の位置が不正です。'" + t.text + "'"); }
                    else if (tokens[i - 1].type == TokenType.LPAR) { throw new KourinException("演算子の位置が不正です。'" + t.text + "'"); }
                    else if (tokens[i - 1].type == TokenType.OPE) { throw new KourinException("演算子が連続しています。'" + t.text + "'"); }
                }
                else if(t.type == TokenType.FUNC && (t as FunctionToken).isPipe)
                {
                    if (!(i > 0 && (tokens[i-1].type == TokenType.RPAR || isOperand(tokens[i - 1])))) {
                        throw new KourinException("関数パイプの位置が不正です。'" + t.text + "'");
                    }
                }
                else if(isOperand(t))
                {
                    if (i > 0 && (tokens[i-1].type == TokenType.RPAR || isOperand(tokens[i-1])))
                        throw new KourinException("リテラル/関数/変数が連続しています。'" + t.text + "'");
                }
                else if (t.type == TokenType.LPAR)
                {
                    perCount++;
                    if (i > 0 && (tokens[i-1].type == TokenType.RPAR || isOperand(tokens[i-1])))
                        throw new KourinException("リテラル/関数/変数が連続しています。'" + t.text + "'");
                }
                else if (t.type == TokenType.RPAR)
                {
                    perCount--;
                    if (i > 0 && tokens[i-1].type == TokenType.LPAR)
                        throw new KourinException("空の()です。");
                }
            }

            if(perCount != 0) {
                throw new KourinException("()の対応がとれていません。");
            }
        }
        
        /// <summary>
        /// 一番外側の[]で区切られた範囲の終わりまでインデクスを進める
        /// </summary>
        /// <param name="script">スクリプト</param>
        /// <param name="startIndex">開始インデクス（[の位置）</param>
        /// <param name="sChar">開始括弧文字</param>
        /// <param name="eChar">終了括弧文字</param>
        /// <param name="separater">分割子。使用しない場合-1</param>
        /// <param name="box">分割子で区切られた括弧内部文字列</param>
        /// <returns>ブロック後のインデクス（]の次）</returns>
        private int skipBlock(string script, int startIndex, char sChar, char eChar, int separater, out string[] box)
        {
            var i=startIndex;
            List<string> list = new List<string>();
            char[] chars = separater>0 ? new char[] {sChar, eChar, (char)separater } : new char[] {sChar, eChar};

            i++;
            bool matchEnd=false;
            while (i < script.Length){
                int count=0;
                int k=i;
                bool matchSep=false;

                while(i<script.Length) {
                    var c = script[i];
                    var c2 = i < script.Length - 1 ? script[i + 1] : '\0';
                    var c3 = i < script.Length - 2 ? script[i + 2] : '\0';
                    if (c==separater && count==0) { matchSep=true; break; }
                    else if(c==eChar && count==0) { matchEnd=true; break; }
                    else if(c==sChar) count++;
                    else if(c==eChar) count--;
                    else if(c==StrChar) {
                        for(i=i+1; ; i++) {
                            if(isStrEndChar(script, i)) break;
                            if(i >= script.Length) throw new KourinException("文字列の終了を検出できませんでした。");
                            //これが出たらバグ
                        }
                    }
                    else if(c=='$' && c2==StrChar && c3=='\n') {
                        for(i=i+3; ; i++) {
                            if(script[i - 2] == '\n' && script[i - 1] == StrChar && script[i] == '$') break;
                            if (i >= script.Length) throw new KourinException("ヒア文字列の終了を検出できませんでした。");
                            //これが出たらバグ
                        }
                    }
                    i++;
                }

                var s = script.Substring(k, i - k);
                if(s != "" && s != " ") { list.Add(s); }
                if(matchEnd) { i++; break; }
                if(matchSep) { i++; }
            }
            if(!matchEnd) throw new KourinException(eChar+"が不足しています。");

            box = list.ToArray();
            return i;
        }

        /// <summary>
        /// 該当箇所が文字列表現の終了位置か否か
        /// </summary>
        private bool isStrEndChar(string text, int i)
        {
            if(i >= text.Length ) return false;
            if(text[i] != StrChar) return false;
            int count=0;
            for(i=i-1; i>=0 && text[i]==EscChar; i--) count++;
            return count % 2 == 0; //\の数によりエスケープかどうか決まる
        }

        //◆━━━━━━━━━━━━━━━━━━━━━━━━━━━━◆

        /// <summary>
        /// 逆ポーランド記法変換
        /// </summary>
        private List<Token> toRPN(List<Token> script){
            var ret = new List<Token>(script.Count);    //結果格納
            var opeStack = new Stack<Token>();  //演算子スタック

            foreach (var token in script)
            {
                if (token.type == TokenType.OPE)
                {   //要素は演算子
                    var to = (OperatorToken)token;

                    //スタック内の優先順位が以上の演算子をすべて結果に移動する。
                    while (true)
                    {
                        if (opeStack.Count == 0) { break; }
                        if (opeStack.Peek().type == TokenType.LPAR) { break; }
                        if (((OperatorToken)opeStack.Peek()).ope.priority < to.ope.priority) { break; }
                        ret.Add(opeStack.Pop());
                    }
                    opeStack.Push(token);
                }
                else if (token.type == TokenType.LPAR)
                {   //要素は左括弧
                    opeStack.Push(token);
                }
                else if (token.type == TokenType.RPAR)
                {   //要素は右括弧
                    //左括弧までの全演算子を結果に格納する。
                    while (opeStack.Count > 0)
                    {
                        var stackOpe = opeStack.Pop();
                        if (stackOpe.type == TokenType.LPAR) { break; }
                        else { ret.Add(stackOpe); }
                    }
                }
                else
                {   //その他（項目）
                    ret.Add(token);
                }
            }
            //スタックに残った演算子をすべて結果に移動する。
            while (opeStack.Count > 0) { ret.Add(opeStack.Pop()); }

            return ret;
        }
               
        //◆━━━━━━━━━━━━━━━━━━━━━━━━━━━━◆

        /// <summary>
        /// トークン実行
        /// </summary>
        /// <param name="tokens">逆ポーランド記法に解析されたトークンリスト</param>
        /// <returns>結果値</returns>
        private object runTokens(List<Token> tokens)
        {
            var stack = new Stack<object>();

            //変数トークンの指定に合う変数テーブルを返す
            Func<VariableToken, Dictionary<string, object>> getVarTable = (token)=>{
                if(!token.isGlobal && token.isScoped) return this.svariables;
                else if(token.isGlobal && !token.isScoped) return this.variables;
                else if(svariables.ContainsKey(token.varName)) return this.svariables;
                else return this.variables;
            };

            //型が変数型なら変数テーブルから値を取得、そうでなければそのまま返す
            Func<object, object> getVarIfItIsVar = (arg)=> {
                if (arg is Variable) {
                    var token = ((Variable)arg).token;
                    var table = getVarTable(token);
                    if (table.ContainsKey(token.varName)) { return table[token.varName]; }
                    else throw new KourinException("変数'"+token.text+"'が初期化されていません");
                }
                else return arg;
            };
            
            //計算実行
            foreach (var token in tokens)
            {
                if (token.type == TokenType.NUM)
                {
                    object box;
                    if (!PresetFunctions.tryNum(token.text, out box)){
                        throw new KourinException("'" + token + "'を数値へ変換できません。");
                    }
                    stack.Push(box);
                }
                else if (token.type == TokenType.STR)
                {
                    stack.Push(token.text);
                }
                else if (token.type == TokenType.FUNC)
                {   //関数を実行して結果をスタック
                    var ft = token as FunctionToken;
                    if(ft.isPipe) stack.Push( callFunction(ft, getVarIfItIsVar(stack.Pop())) );
                    else stack.Push(callFunction(ft, null));
                }
                else if (token.type == TokenType.OPE)
                {
                    var ope = ((OperatorToken)token).ope;
                    var args = new object[2];
                    args[1] = stack.Pop(); //後入れの方がうしろ
                    args[0] = stack.Pop();

                    if (ope.str == "=")
                    {   //代入演算
                        var vari = (Variable)args[0];
                        getVarTable(vari.token)[vari.token.varName] = getVarIfItIsVar(args[1]);
                        stack.Push(null);
                    }
                    else
                    {   //それ以外は対応する関数を呼ぶ
                        args[0] = getVarIfItIsVar(args[0]);
                        args[1] = getVarIfItIsVar(args[1]);
                        stack.Push(callFunction(ope.function, args));
                    }
                }
                else if (token.type == TokenType.VAR)
                {   //変数は値ではなく変数クラスを作ってスタック（出現時点では代入か取得か不明）
                    stack.Push(new Variable((VariableToken)token));
                }
                else if (token.type == TokenType.MKFUNC)
                {   //ブロック宣言
                    var t = (MakeFunctionToken)token;
                    if (t.funcName != "") {
                        //新規関数登録
                        var fd = functionTable;
                        if(fd.ContainsKey(t.funcName) && !(fd[t.funcName] is ScriptFunction)) {
                            throw new KourinException("静的登録関数は上書きできません。関数名:" + t.funcName);
                        }
                        setFunction(new ScriptFunction(t.funcName, t.subscript));
                        stack.Push(null);
                    } else {
                        //関数名無し=スクリプトブロック。すぐに実行。
                        stack.Push(rideScript(t.subscript, "Script block"));
                    }
                }
            }

            if (stack.Count > 1) {
                throw new KourinException("単一の演算結果が得られませんでした。");
            }
            if (stack.Count == 0){ return null; }
            return getVarIfItIsVar(stack.Pop());
        }

        /// <summary>
        /// 関数呼び出し
        /// </summary>
        private object callFunction(FunctionToken token, object pipedObj)
        {
            var args = token.args;
            var fname = token.funcName;
            object[] args_obj = new object[args.Count + (token.isPipe ? 1 : 0)];

            if (fname.Equals("IF", StringComparison.OrdinalIgnoreCase))
            {
                //特別関数IF：引数を後演算（IF[$A==NULL,0,$A*5]のような式がエラーにならないよう）
                if (token.isPipe) throw new KourinException("IF関数にパイプは利用できません。");

                //条件のみ先に実行
                if (args.Count > 0) args_obj[0] = rideOne(args[0]);
                if (args.Count > 1) args_obj[1] = args[1];
                if (args.Count > 2) args_obj[2] = args[2];
                return rideOne(callFunction(fname, args_obj).ToString());
            }
            else if (fname.Equals("REPEAT", StringComparison.OrdinalIgnoreCase))
            {
                //特別関数：第二引数の文を繰り返し実行
                if (token.isPipe) throw new KourinException("REPEAT関数にパイプは利用できません。");

                //回数条件
                if (args.Count > 0) args_obj[0] = rideOne(args[0]);
                //引数チェックとして呼ぶ
                callFunction(fname, args_obj);
                //繰り返し実行
                object ret = null;
                for(var c=0; c < (int)args_obj[0]; c++){
                    ret=rideOne(args[1]);
                    if(ret is ReturnedObject) break;
                }
                return ret;
            }
            else if (fname.Equals("WHILE", StringComparison.OrdinalIgnoreCase))
            {
                //特別関数：第一引数の実行結果がtrueのあいだ第二引数の文を繰り返し実行
                if(token.isPipe) throw new KourinException("WHILE関数にパイプは利用できません。");

                callFunction(fname, args_obj); //引数チェックのみ
                object ret = null;
                while (true) {
                    object loop = rideOne(args[0]);
                    if(!(loop is bool)) 
                        throw new KourinException("WHILE関数の条件式がbool以外を返しました。");
                    if((bool)loop == false) break;
                    else ret = rideOne(args[1]);
                    if(ret is ReturnedObject) break;
                }
                return ret;
            }
            else if (fname.Equals("TOSCRIPT", StringComparison.OrdinalIgnoreCase))
            {
                //特別関数：引数内のスクリプトをそのまま文字列として返す
                if (token.isPipe) throw new KourinException("TOSCRIPT関数にパイプは利用できません。");
                if (args.Count > 0) return args[0];
                else return "";
            }
            else if (fname.Equals("DEPLOY", StringComparison.OrdinalIgnoreCase))
            {
                //特別関数：文字列に変数を展開する
                if(token.isPipe) args_obj[0] = pipedObj;
                else if(args.Count > 0) args_obj[0] = rideOne(args[0]);

                if(!(args_obj[0] is string)) throw new KourinException("DEPLOY関数の引数が文字列ではありません。");

                var s = (string)args_obj[0];
                var sb = new StringBuilder(s.Length);
                int pre = 0;
                var ptn = @"{\s*(\$\$?[^\s\$\{\}]+)\s*}";
                foreach (Match m in Regex.Matches(s, ptn)) {
                    var val = rideOne(m.Value);
                    sb.Append(s.Substring(pre, m.Index-pre));
                    sb.Append(val);
                    pre = m.Index + m.Length;
                }
                sb.Append(s.Substring(pre, s.Length-pre));
                return sb.ToString();
            }
            else
            {
                //引数部分を再帰で先に演算する
                if (token.isPipe) args_obj[0] = pipedObj;
                for (int i = 0; i < args.Count; i++)
                    args_obj[token.isPipe ? i+1 : i] = rideOne(args[i]);

                return callFunction(fname, args_obj);
            }
        }

        /// <summary>
        /// 関数呼び出し
        /// </summary>
        private object callFunction(String funcName, object[] args)
        {
            if(!functionTable.ContainsKey(funcName)) {
                throw new KourinException("関数'" + funcName + "'が見つかりません。");
            }

            var func = functionTable[funcName];
            object ret;
            if(func is ScriptFunction) { //スクリプト関数
                //スコープ変数を退避
                var table = this.svariables;
                //スコープ変数に引数を追加
                this.svariables = new Dictionary<string, object>();
                for(var i=0; i<args.Length; i++) this.svariables["args"+i]=args[i];
                //実行
                ret = rideScript(((ScriptFunction)func).script, func.name);
                //スコープ変数を戻す
                this.svariables = table;
                //RETURN戻りの場合、内容を復元する
                if(ret is ReturnedObject) ret = ((ReturnedObject)ret).value;
            }
            else { //静的登録関数
                ret = func.execute(args);
            }
            return ret;
        }

        /// <summary>
        /// 変数用クラス
        /// </summary>
        private class Variable
        {
            public VariableToken token;
            public Variable(VariableToken token) { this.token = token; }
        }

        //◆━━━━━━━━━━━━━━━━━━━━━━━━━━━━◆

        private class Token
        {
            public TokenType type;
            public string text;

            public Token(TokenType type, string text)
            {
                this.type = type;
                this.text = text;
            }
            public override string ToString()
            {
                return text;
            }
        }

        private enum TokenType
        {
          //数   文   関数  演算子 (     )     変数 関数宣言
            NUM, STR, FUNC, OPE,   LPAR, RPAR, VAR, MKFUNC
        }

        private class OperatorToken : Token
        {
            public Operator ope;
            public OperatorToken(Operator ope, string text) : base(TokenType.OPE, text)
            {
                this.ope = ope;
            }
        }
        private class VariableToken : Token
        {
            public string varName;
            public bool isScoped = false;
            public bool isGlobal = false;
            public VariableToken(string varName, string text) : base(TokenType.VAR, text)
            {
                this.varName = varName;
            }
        }
        private class FunctionToken : Token
        {
            public string funcName;
            public List<string> args;
            public bool isPipe;
            public FunctionToken(string funcName, string text, bool isPipe) : base(TokenType.FUNC, text)
            {
                this.funcName = funcName;
                this.isPipe = isPipe;
                args = new List<string>(5);
            }
        }
        private class MakeFunctionToken : Token
        {
            public string funcName;
            public string subscript;
            public MakeFunctionToken(string funcName, string subscript, string text)
                : base(TokenType.MKFUNC, text)
            {
                this.funcName = funcName;
                this.subscript = subscript;
            }
        }

        //◆━━━━━━━━━━━━━━━━━━━━━━━━━━━━◆

        private class Operator
        {
            public string str;
            public int priority;
            public string function;

            private static char[] chars = { '+', '-', '*', '/', '%', '|', '&', '=', '!', '>', '<', ';' };
            private static HashSet<char> charshash = new HashSet<char>(chars);

            public static Operator[] operators = {
                     new Operator(){ str="*",  priority=5, function=PresetFunctions.mul.name},
                     new Operator(){ str="/",  priority=5, function=PresetFunctions.div.name},
                     new Operator(){ str="%",  priority=5, function=PresetFunctions.mod.name},
                     new Operator(){ str="+",  priority=4, function=PresetFunctions.add.name},
                     new Operator(){ str="-",  priority=4, function=PresetFunctions.sub.name},

                     new Operator(){ str="&",  priority=3, function=PresetFunctions.cat.name},

                     new Operator(){ str=">",  priority=2, function=PresetFunctions.over.name},
                     new Operator(){ str=">=", priority=2, function=PresetFunctions.eover.name},
                     new Operator(){ str="<",  priority=2, function=PresetFunctions.under.name},
                     new Operator(){ str="<=", priority=2, function=PresetFunctions.eunder.name},
                     new Operator(){ str="==", priority=1, function=PresetFunctions.equal.name},
                     new Operator(){ str="!=", priority=1, function=PresetFunctions.nequal.name},

                     new Operator(){ str="&&", priority=0, function=PresetFunctions.and.name},
                     new Operator(){ str="||", priority=0, function=PresetFunctions.or.name},
                                                 
                     new Operator(){ str="=", priority=-1},

                     //;は直列実行演算子。左辺, 右辺と評価され、右辺の値を結果値として返す。
                     //全ての演算子より優先度が低いが、括弧()よりは先に判定される。
                     new Operator(){ str=";", priority=-99, function=PresetFunctions.executionAll.name}};

            public static Operator parse(string str)
            {
                return operators.FirstOrDefault(item => item.str == str);
            }
            public static bool isOerator(char c)
            {
                return charshash.Contains(c);
            }
        }
    }

}
