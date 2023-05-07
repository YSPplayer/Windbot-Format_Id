using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.SQLite;
using System.Threading.Tasks;
using System.IO;
using System.Data;

namespace WindBot_Format_Id
{
    class Program
    {
        private static List<string> ReadNames(string DB,List<long> codes)
        {
            //如果文件存在
            if (File.Exists(DB))
            {
                List<string> names = new List<string>(codes.Count());
                using (SQLiteConnection sqliteconn = new SQLiteConnection(@"Data Source=" + DB + ";version = 3; Character Set = utf8"))
                {
                    sqliteconn.Open();
                    using (SQLiteTransaction trans = sqliteconn.BeginTransaction())
                    {
                        using (SQLiteCommand sqlitecommand = new SQLiteCommand(sqliteconn))
                        {
                            foreach (var code in codes)
                            {
                                //获取我们卡号匹配的名称
                                string SQLstr = $"SELECT texts.name FROM texts WHERE texts.id={code}"; ;
                                sqlitecommand.CommandText = SQLstr;
                                using (SQLiteDataReader reader = sqlitecommand.ExecuteReader())
                                {
                                    
                                    while (reader.Read()) {
                                        string str = reader.GetString(reader.GetOrdinal("name"));
                                        //特殊字符替换
                                        str = str.Replace(" ", "");
                                        str = str.Replace(".", "");
                                        str = str.Replace("-", "");
                                        str = str.Replace("·", "");
                                        names.Add(str);
                                    }
                                    reader.Close();
                                }
                            }
                        }
                        trans.Commit();
                    }
                    sqliteconn.Close();
                    return names;
                }
            }
            else
            {
                Console.WriteLine("cdb文件不存在！");
                return null;
            }
        }
        private static void Main(string[] args)
        {
            Console.WriteLine("使用方法:把要转换的ydk放在ydk文件夹下即可");
            Console.WriteLine("注意：如果有多个ydk文件，程序只会读取并转换第一个");
            //我们只读取首个位置的文件
            string keyPath = "./deck";
            string[] files = Directory.GetFiles(keyPath, "*.ydk");
            if (files.Length <= 0)
            {
                Console.WriteLine("deck文件夹下没有deck文件！");
                return;
            }
            string AI_deckPath = files[0];
            string deckPath = AI_deckPath;
            deckPath.Replace("AI_", "");
            //只读取首个文件
            string filePath = AI_deckPath;
            bool isMain = false, isExtra = false;
            List<long> main_codes = new List<long>();
            List<long> extra_codes = new List<long>();
            int value;
            using (StreamReader sr = new StreamReader(filePath))
            { // 使用 StreamReader 打开文件
                while (!sr.EndOfStream)
                {
                    string line = sr.ReadLine();  // 逐行读取数据
                    if (line.StartsWith("#main"))
                    {   // 如果找到 #main 标识符，则表示开始进入主要内容区域
                        isMain = true;
                        isExtra = false;
                    }
                    else if (line.StartsWith("#extra"))
                    { // 如果找到 #extra 标识符，则表示进入额外内容区域
                        isMain = false;
                        isExtra = true;
                    }
                    else if (line.StartsWith("!side"))
                    { // side不加入
                        break;
                    }
                    else if (isMain && !isExtra)
                    { // 如果当前行不在 #main 到 #extra 区间范围内，则忽略该行
                        if (int.TryParse(line, out value))
                        {  
                            main_codes.Add(value);    // 解析成功则将该整数添加到 List<int> 中
                        }
                    }
                    else if (!isMain && isExtra) 
                    {
                        if (int.TryParse(line, out value))
                        {
                            extra_codes.Add(value);  
                        }
                    }
                }
                sr.Close();
            }
            string dbPath = @"./cdb/cards.cdb";
            //获取我们的名称
            List<string> main_names = ReadNames(dbPath, main_codes);
            List<string> extra_names = ReadNames(dbPath, extra_codes);
            if (main_names == null || extra_names == null)  return;
            if (main_names.Count() != main_codes.Count() || extra_names.Count() != extra_codes.Count())
            {
                Console.WriteLine("cards.cdb中找不到ydk中部分匹配的卡号，请更新cdb或替换合适的ydk!");
                Console.ReadLine();
                return;
            }
            string deckName = Path.GetFileNameWithoutExtension(deckPath);
            string aiDeckName = Path.GetFileNameWithoutExtension(AI_deckPath);
            //创建我们的cs文件
            string filename = $"./output/{deckName}Executor.cs";
            using (StreamWriter writer = new StreamWriter(filename))
            {
                writer.WriteLine(GetScript(deckName, aiDeckName, main_codes,extra_codes,main_names,extra_names));
                writer.Close();
                Console.WriteLine("创建成功!");
            }
            Console.ReadLine();
        }
        //获取我们的脚本
        private static string GetScript(string name,string aiName,List<long> mainCodes,
            List<long> extraCodes,List<string> mainNames,List<string>extraNames)
        {
            //去重
            List<long> unmainCodes = new List<long>();
            List<long> unextraCodes = new List<long>();
            string idStr = "";
            idStr += "          //main code\n";
            for (int i = 0; i < mainCodes.Count(); ++i) 
            {
                if (unmainCodes.Contains(mainCodes[i])) continue;
                unmainCodes.Add(mainCodes[i]);
                idStr += $"          public const int {mainNames[i]} = {mainCodes[i]};\n";
            }
            idStr += "\n";
            idStr += "          //extra code\n";
            for (int i = 0; i < extraCodes.Count(); ++i)
            {
                if (unextraCodes.Contains(extraCodes[i])) continue;
                unextraCodes.Add(extraCodes[i]);
                idStr += $"          public const int {extraNames[i]} = {extraCodes[i]};\n";
            }

            string caseStr = "";
            for (int i = 0; i < unmainCodes.Count(); ++i)
            {
                int index = -1;
                int count = 0;
                //获取索引
                for (int j = 0; j < mainCodes.Count(); j++)
                {
                    if (unmainCodes[i] == mainCodes[j])
                    {
                        index = j;
                        //获取卡组剩余相同卡片的数量
                        ++count;
                    }
                }
                //返回索引
                caseStr += $"                case CardId.{mainNames[index]}\n:                    return Bot.GetRemainingCount(CardId.{mainNames[index]}, {count});\n";
            }
            caseStr += "                default:\n                    return 0;\n";
            return "using YGOSharp.OCGWrapper.Enums;\nusing System.Collections.Generic;\nusing System.Linq;" +
                "\nusing WindBot;\nusing WindBot.Game;\nusing WindBot.Game.AI;\nnamespace WindBot.Game.AI.Decks" +
                "\n{\n" + $"   [Deck(\"{name}\", \"{aiName}\")]\n" + $"   class {name}Executor : DefaultExecutor\n" 
                + "   {\n" + "      public class CardId\n      {\n" + idStr + "      }\n"
                + $"      public {aiName}Executor(GameAI ai, Duel duel): base(ai, duel)\n" + "      {\n\n      }\n"
                + "      private int CheckRemainInDeck(int id)\n      {\n"
                + "          switch (id)\n          {\n" + caseStr + "          }\n      }\n   }\n}";
        }
    }
}
