using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Web.Http;
using System.Xml;
using System.Xml.Linq;
using TouNaoWangZhe.Models;

namespace TouNaoWangZhe.Controllers
{
    public class TouNaoApiController : ApiController
    {
        private List<int> BaiduCount = new List<int> { };
        private List<int> _360Count = new List<int> { };
        private List<int> HebingCount = new List<int> { };
        private String[] AnswerList = new string[] { "A", "B", "C", "D" };
        private bool Is360Search = true;
        private int BaiduMaxIndex;
        private int _360MaxIndex;
        private int HebingMaxIndes;
        private List<int> SpiltCount = new List<int> { };
        private List<string> MultipleOptions = new List<string> { };
        private bool IsdDoubleOptions = false;
        [HttpPost]
        public IHttpActionResult GetAnswerFromBaidu(TNModel paras)
        {
            List<TNWZ> list = new List<TNWZ>();
            using (TouNaoEntities tnwz = new TouNaoEntities())
            {
                list = tnwz.TNWZ.ToList();
                TNWZ dbmodel = list.FirstOrDefault(p=>p.Quiz==paras.data.quiz);
                if (dbmodel != null && dbmodel.Result == "正确")
                {
                    saveAnswer(dbmodel.Answer);
                    return Json(new { code = 200 });
                }
                if(dbmodel != null)
                { 
                    dbmodel.Answer= GetNewAnswer(dbmodel);
                    dbmodel.HistoryAnswer = dbmodel.HistoryAnswer.Trim()+dbmodel.Answer.Trim();
                    saveAnswer(dbmodel.Answer);
                    if (tnwz.SaveChanges() > 0)
                    {
                        return Json(new { code = 200,msg="更新答案成功！" });
                    }
                }
            }

            #region 清除答案文件
            DirectoryInfo dir = new DirectoryInfo("d:\\daan");
            FileSystemInfo[] fileinfo = dir.GetFileSystemInfos();  //返回目录中所有文件和子目录
            foreach (FileSystemInfo i in fileinfo)
            {
                if (i is DirectoryInfo)            //判断是否文件夹
                {
                    DirectoryInfo subdir = new DirectoryInfo(i.FullName);
                    subdir.Delete(true);          //删除子目录和文件
                }
                else
                {
                    File.Delete(i.FullName);      //删除指定文件
                }
            }
            #endregion

            #region 双答案处理         
            var doubleOptions = paras.data.options.ToList().Where(o => o.Contains("、")).ToList();
            if (doubleOptions.Any())
            {
                IsdDoubleOptions = true;
                foreach (string item in paras.data.options)
                {
                    if (item.Contains("、"))
                    {
                        string[] splitList = item.Split('、');
                        SpiltCount.Add(splitList.Length);
                        MultipleOptions.AddRange(splitList);
                    }
                    else
                    {
                        SpiltCount.Add(1);
                        MultipleOptions.Add(item);
                    }
                }
            }
            #endregion

            #region 百度搜索\360搜索--》词频锁定答案
            string[] Searchurl = new[] { "http://www.baidu.com/s?wd={0}&timeout=2", "https://www.so.com/s?ie=utf-8&fr=none&src=360sou_newhome&q={0}" };
            if (!string.IsNullOrEmpty(paras.data.quiz))
            {
                for (int i = 0; i < Searchurl.Length; i++)
                {
                    Searchurl[i] = string.Format(Searchurl[i], paras.data.quiz);
                }
            }
            else
            {
                for (int i = 0; i < Searchurl.Length; i++)
                {
                    Searchurl[i] = string.Format(Searchurl[i], "中国位于哪个大洲？");
                }
            }
            foreach (string items in Searchurl)
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(items);
                request.Method = "Get";
                request.ContentType = "text/html;charset=utf-8";

                string str = "";
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    Stream stream = response.GetResponseStream();
                    StreamReader sr = new StreamReader(stream, Encoding.UTF8);
                    StringBuilder sb = new StringBuilder();
                    sb.Append(sr.ReadToEnd());
                    str = sb.ToString();
                }
                int strLength = str.Length;
                if (!string.IsNullOrEmpty(str) && paras.data.options.Length > 0)
                {
                    if (!IsdDoubleOptions)
                    {
                        foreach (var item in paras.data.options)
                        {
                            int result;
                            string strReplace = str.Replace(item, "");
                            int strReplaceLength = strReplace.Length;
                            int diffrent = strLength - strReplaceLength;
                            int itemLength = item.ToString().Length;
                            result = diffrent > 0 ? diffrent / itemLength : 0;
                            if (Is360Search && items.Contains("www.so.com"))
                            { _360Count.Add(result); }
                            else
                            { BaiduCount.Add((result)); }
                        }
                    }
                    else
                    {
                        int SpiltSpiltCountLength = 0;
                        for (int j = 0; j < SpiltCount.Count; j++)
                        {
                            int result = 0;
                            for (int i = 0; i < SpiltCount[j]; i++)
                            {
                                string Smallitem = MultipleOptions[SpiltSpiltCountLength + i];
                                string strReplace = str.Replace(Smallitem, "");
                                int strReplaceLength = strReplace.Length;
                                int diffrent = strLength - strReplaceLength;
                                int itemLength = MultipleOptions[i].ToString().Length;
                                result = (diffrent > 0 ? diffrent / itemLength : 0) + result;
                            }
                            if (Is360Search && items.Contains("www.so.com"))
                            { _360Count.Add(result); }
                            else
                            { BaiduCount.Add((result)); }
                            SpiltSpiltCountLength = SpiltCount[j] + SpiltSpiltCountLength;
                        }
                    }
                }
            }
            BaiduMaxIndex = BaiduCount.FindIndex(c => c.Equals(BaiduCount.Max()));
            _360MaxIndex = _360Count.FindIndex(c => c.Equals(_360Count.Max()));
            #endregion

            #region 百度搜索结果不等于360搜索结果处理
            bool IsEqual = BaiduMaxIndex == _360MaxIndex;
            if (!IsEqual)
            {
                for (int i = 0; i < 4; i++)
                {
                    HebingCount.Add(BaiduCount[i] + _360Count[i]);
                }
                HebingMaxIndes = HebingCount.FindIndex(c => c.Equals(HebingCount.Max()));
            }
            #endregion

            string Answer = IsEqual ? AnswerList[BaiduMaxIndex] : AnswerList[HebingMaxIndes];
            saveAnswer(Answer);


            using (TouNaoEntities tn = new TouNaoEntities())
            {
                var dbmodel = list.FirstOrDefault(p => p.Quiz == paras.data.quiz);
                if (dbmodel == null)
                {
                    TNWZ model = new TNWZ();
                    model.Answer = Answer;
                    model.Options = string.Join(",", paras.data.options);
                    model.Quiz = paras.data.quiz;
                    model.CreateDate = DateTime.Now;
                    model.Num = paras.data.num;
                    model.HistoryAnswer = Answer;

                    tn.TNWZ.Add(model);
                    if (tn.SaveChanges() > 0)
                    {
                        return Json(new { code = 200, msg = "保存成功！" });
                    }
                }
            }
            return Json(new { code = 200 });

        }

        [HttpPost]
        public IHttpActionResult updateresult(updateResult updatemodel)
        {
            using (TouNaoEntities tn = new TouNaoEntities())
            {
                if (updatemodel.data.yes)
                {
                    TNWZ model = tn.TNWZ.Where(t => t.Num == updatemodel.data.num).OrderByDescending(t => t.CreateDate).ToList()[0];
                    model.Result = "正确";
                    if (tn.SaveChanges() > 0)
                    {
                        return Json(new { code = 200, msg = "保存成功！" });
                    }
                }
                else
                {
                    TNWZ model = tn.TNWZ.Where(t => t.Num == updatemodel.data.num).OrderByDescending(t => t.CreateDate).ToList()[0];
                    model.Result = "正确";
                    model.Answer = AnswerList[updatemodel.data.answer-1];
                    model.HistoryAnswer = model.HistoryAnswer.Trim() + AnswerList[updatemodel.data.answer - 1].Trim();
                    if (tn.SaveChanges() > 0)
                    {
                        return Json(new { code = 200, msg = "保存成功！" });
                    }
                }
                return Json(new { code = 200 });
            }
        }

        public static void saveAnswer(string Answer)
        {
            FileStream fs = new FileStream("d:\\daan\\" + Answer + ".txt", FileMode.OpenOrCreate);
            StreamWriter sw = new StreamWriter(fs);
            sw.WriteLine(Answer);
            sw.Close();
        }
        public string GetNewAnswer(TNWZ model)
        {
            Random r = new Random();
            int index=r.Next(4);
            if (!model.HistoryAnswer.Contains(AnswerList[index]))
            {
                return AnswerList[index];
            }
            else
            {
                return GetNewAnswer(model);
            }                        
             
        }
        
    }
    public class TNModel
    {
        public TNdata data { get; set; }
    }
    public class TNdata
    {
        public string quiz { get; set; }
        public string[] options { get; set; }
        public string num { get; set; }
    }
    public class updateResult
    {
        public updatedata data { get; set; }
    }
    public class updatedata
    {
        public bool yes { get; set; }
        public string totalScore { get; set; }
        public string num { get; set; }
        public int answer { get; set; }
    }
}
