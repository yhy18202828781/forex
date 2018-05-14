using System;
using System.IO;
using System.Linq;
using System.Collections;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;

//震荡策略，仅RSI指标

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.ChinaStandardTime, AccessRights = AccessRights.FileSystem)]
    public class KtEa03OscillationOnlyRsi : Robot
    {
        //CCI指标期数
        [Parameter("PeriodsCci", DefaultValue = 20)]
        public int PeriodsCci { get; set; }

        //RSI指标期数
        [Parameter("PeriodsRsi", DefaultValue = 14)]
        public int PeriodsRsi { get; set; }

        //下单数量
        [Parameter("order_volume", DefaultValue = 100000)]
        public int order_volume { get; set; }

        //是否移动（追踪）止损
        [Parameter("has_trailing_stop", DefaultValue = true)]
        public bool has_trailing_stop { get; set; }

        //止损点
        [Parameter("stop_loss_pips", DefaultValue = 10)]
        public int stop_loss_pips { get; set; }

        //止盈点
        [Parameter("take_profit_pips_base", DefaultValue = 4)]
        public double take_profit_pips_base { get; set; }

        //策略运行开始的小时数
        [Parameter("ea_start_hour", DefaultValue = 23)]
        public int ea_start_hour { get; set; }

        //策略运行结束的小时数
        [Parameter("ea_end_hour", DefaultValue = 12)]
        public int ea_end_hour { get; set; }


        private double take_profit_pips;

        //策略是否启动
        private bool qidong = false;
        //下单操作类型，1:Sell,2:Buy,0:未知
        private int order_type = 0;
        //上一单下单操作类型，1:Sell,2:Buy,0:未知
        private int last_order_type = 0;
        //订单标签
        public string order_label = "first_order";
        //下单时间戳
        private long order_timestamp = 0;
        //平仓时的时间戳
        private long ping_cang_timestamp = 0;
        //是否在没找到订单时设置了平仓时间戳
        private bool is_set_ping_cang_timestamp_when_has_not_order = false;
        //是否下过订单，只要下过一单都算;
        private bool has_order = false;
        //m5数据路径
        //private string path_m5 = "D:\\csharp\\m5_rsi.txt";
        //t10数据路径
        //private string path_t10 = "D:\\csharp\\t10.txt";
        //m5当前值
        private double curr_m5_val;
        //下单时m5的值
        private double m5_val_when_order = 50;
        //下单前m5的最低值
        private double m5_min_val_before_order = 50;
        //下单前m5的最高值
        private double m5_max_val_before_order = 50;
        //下单时的价格
        private double price_when_order = 0;
        //是否第一次价格变动，即是否判断是否刚启动EA
        private bool is_first_price_change = true;
        //m5最低值到当前值的价格数组
        private ArrayList m5_min_to_now_price_al = new ArrayList();
        //m5最高值到当前值的价格数组
        private ArrayList m5_max_to_now_price_al = new ArrayList();
        //日志路径
        private string path_log = "C:\\csharp\\log_64.txt";
        //日志字符串
        private string log_str = "";

        protected override void OnStart()
        {
            Print("EA启动...");
        }

        protected override void OnTick()
        {
            //int curr_hour = DateTime.Now.Hour;
            int curr_hour = MarketSeries.OpenTime.LastValue.Hour;
            if (curr_hour >= ea_end_hour && curr_hour < ea_start_hour)
            {
                Position position = Positions.Find(order_label);
                if (position != null)
                {
                    //不在运行时段内，且还存在订单的情况，无条件平仓
                    var mao_li_run = position.GrossProfit;
                    ClosePosition(position);
                    Print("非运行时段，直接平仓当前存在的订单，毛利润为：{0}", mao_li_run);
                    return;
                }
                Print("目前为该策略非运行时段，请休息会再来看看哈~~~");
                return;
            }

            Check();
            //价格只要变化一次之后，就将该值置为false
            is_first_price_change = false;
        }

        protected override void OnStop()
        {
            Print("EA停止...");
        }

        //主逻辑
        private void Check()
        {
            //价格每变动一次，就往俩数组追加一个值
            //m5_max_to_now_price_al.Add(Symbol.Bid);
            //m5_min_to_now_price_al.Add(Symbol.Ask);



            //string[] data_m5 = getLatestData(path_m5, 1000);
            string[] data_m5 = GetIndicatorsLatestData("rsi", 1000);

            curr_m5_val = GetCurrM5Val(data_m5);



            //判断是否需要将最低或最高值更新
            if (curr_m5_val < 50)
            {
                if (is_first_price_change)
                {
                    //如果是刚启动EA，将当前值设为最低值
                    m5_min_val_before_order = curr_m5_val;
                }
                else if (curr_m5_val < m5_min_val_before_order)
                {
                    //如果不是刚启动EA，且当前值比最小值还小，则将最小值更新为当前值
                    m5_min_val_before_order = curr_m5_val;
                }
            }
            else
            {
                if (is_first_price_change)
                {
                    //如果是刚启动EA，将当前值设为最高值
                    m5_max_val_before_order = curr_m5_val;
                }
                else if (curr_m5_val > m5_min_val_before_order)
                {
                    //如果不是刚启动EA，且当前值比最高值还大，则将最高值更新为当前值
                    m5_max_val_before_order = curr_m5_val;
                }
            }

            log_str = "curr_m5_val:" + curr_m5_val.ToString() + ",m5_min_val_before_order:" + m5_min_val_before_order.ToString() + ",m5_max_val_before_order:" + m5_max_val_before_order.ToString();
            Print(log_str);
            WriteTextToFile(log_str);

            //重置启动值为false
            qidong = false;

            //查找订单
            Position position = Positions.Find(order_label);
            if (position != null)
            {
                //判断利润是否达到止盈点
                var mao_li_run = position.GrossProfit;
                if (position.Pips >= take_profit_pips)
                {
                    //平仓
                    ClosePosition(position);
                    log_str = "达到利润点，平仓成功,该订单的毛利润为：" + mao_li_run.ToString();
                    Print(log_str);
                    WriteTextToFile(log_str);
                    return;
                }

                //如果没有达到利润点平仓
                if (last_order_type == 2 && curr_m5_val <= 40 && curr_m5_val <= m5_val_when_order - 3 && Symbol.Ask < price_when_order)
                {
                    log_str = "当前m5_rsi值<=下单时-3，且价格低于Buy单入场价，平仓（curr_m5_val:" + curr_m5_val.ToString() + ",m5_val_when_order:" + m5_val_when_order.ToString() + ",Symbol.Ask:" + Symbol.Ask.ToString() + ",price_when_order:" + price_when_order.ToString() + "）";
                    Print(log_str);
                    WriteTextToFile(log_str);
                    ClosePosition(position);
                    return;
                }
                if (last_order_type == 1 && curr_m5_val >= 60 && curr_m5_val >= m5_val_when_order + 3 && Symbol.Bid > price_when_order)
                {
                    log_str = "当前m5_rsi值>=下单时+3，且价格高于Sell单入场价，平仓（curr_m5_val:" + curr_m5_val.ToString() + ",m5_val_when_order:" + m5_val_when_order.ToString() + ",Symbol.Bid:" + Symbol.Bid.ToString() + ",price_when_order:" + price_when_order.ToString() + "）";
                    Print(log_str);
                    WriteTextToFile(log_str);
                    ClosePosition(position);
                    return;
                }

                //如果没有达到利润点平仓,判断是否有下单机会
                IsFaDongCeLue(data_m5, true);

                //若达到反向下单的条件，则先对上一单做平仓操作
                if (qidong == true && order_type != last_order_type)
                {
                    ClosePosition(position);
                    log_str = "达到反向下单条件，平仓成功,该订单的毛利润为：" + mao_li_run.ToString() + "（curr_m5_val:" + curr_m5_val.ToString() + ",m5_min_val_before_order:" + m5_min_val_before_order.ToString() + ",m5_max_val_before_order:" + m5_max_val_before_order.ToString() + "）";
                    Print(log_str);
                    WriteTextToFile(log_str);
                }

                return;
            }
            else
            {
                //没找到订单的情况
                if (is_set_ping_cang_timestamp_when_has_not_order == false && has_order == true)
                {
                    //如果在上一单平仓之后没有设置过时间戳，而且,系统至少下过一单（首单不用设置，因为，根本就没有订单，何来平仓！！！）
                    ping_cang_timestamp = GetCurrTimeStamp();
                    is_set_ping_cang_timestamp_when_has_not_order = true;
                }
            }

            if (qidong == false)
            {
                IsFaDongCeLue(data_m5, false);
            }

            if (qidong == false)
            {
                Print("目前没有较好的时机，继续观察");
                return;
            }

            if (order_type == 1)
            {
                //做Sell单的情况
                log_str = "哇哦，达到做Sell单条件了，即将下Sell单";
                Print(log_str);
                WriteTextToFile(log_str);
                PlaceOrder(TradeType.Sell);
            }
            else if (order_type == 2)
            {
                //做Buy单的情况
                log_str = "哇哦，达到做Buy单条件了，即将下Buy单";
                Print(log_str);
                WriteTextToFile(log_str);
                PlaceOrder(TradeType.Buy);
            }
        }

        //返回指标最近num个值，结果为数组
        private string[] GetIndicatorsLatestData(string indicator, int num = 0)
        {
            string[] res = new string[0];
            int len = 0;
            int index = 0;
            if (indicator == "cci")
            {
                var idtr = Indicators.CommodityChannelIndex(PeriodsCci);
                len = idtr.Result.Count;
                if (num > 0 && num > len)
                {
                    num = len;
                }
                else if (num == 0)
                {
                    num = len;
                }
                res = new string[num];
                for (var i = len - num; i < len; i++)
                {
                    res[index] = GetCurrTimeStamp().ToString() + "|" + idtr.Result[i].ToString();
                    index++;
                }
            }
            else if (indicator == "rsi")
            {
                var idtr = Indicators.RelativeStrengthIndex(MarketSeries.Close, PeriodsRsi);
                len = idtr.Result.Count;
                if (num > 0 && num > len)
                {
                    num = len;
                }
                else if (num == 0)
                {
                    num = len;
                }
                res = new string[num];
                for (var i = len - num; i < len; i++)
                {
                    res[index] = GetCurrTimeStamp().ToString() + "|" + idtr.Result[i].ToString();
                    index++;
                }
            }
            return res;
        }

        //返回文件最后num行数据，结果为数组（若num=0，则返回全部数据）
        private string[] getLatestData(string path, int num = 0)
        {
            try
            {
                using (StreamReader sr = new StreamReader(path))
                {
                    string res = sr.ReadToEnd();
                    string[] arr = res.Split(new char[] 
                    {
                        '\n'
                    });
                    if (num == 0)
                    {
                        return arr.Skip(0).Take(arr.Length - 1).ToArray();
                    }
                    else
                    {
                        return arr.Skip(arr.Length - num - 1).Take(num).ToArray();
                    }
                }
            } catch (Exception e)
            {
                Print("An error occurred:{0}", e.Message);
            }

            return new string[] 
            {
                            };
        }

        //获取当前时间戳
        private long GetCurrTimeStamp()
        {
            long currentTicks = DateTime.Now.Ticks;
            DateTime dtFrom = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            long currentMillis = (currentTicks - dtFrom.Ticks) / 10000 - (8 * 3600 * 1000);
            return currentMillis;
        }

        //获取当前m5值
        private double GetCurrM5Val(string[] data, int type = 1)
        {
            if (type == 1)
            {
                //直接返回最新的一个值
                string[] tmp_str_arr = data[data.Length - 1].Split('|');
                return Convert.ToDouble(tmp_str_arr[1]);
            }
            else
            {
                //返回最近几个值的平均值
                double sum = 0;
                int num_of_val = 8;
                for (var i = data.Length - 1; i >= data.Length - num_of_val; i--)
                {
                    string[] tmp_str_arr = data[i].Split('|');
                    sum += Convert.ToDouble(tmp_str_arr[1]);
                }
                return sum / num_of_val;
            }
        }

        //下单操作
        private void PlaceOrder(TradeType tt)
        {
            var result = ExecuteMarketOrder(tt, Symbol, order_volume, order_label, stop_loss_pips, null, null, "test", has_trailing_stop);
            if (result.IsSuccessful)
            {
                log_str = "下单时，curr_m5_val:" + curr_m5_val.ToString() + ",m5_min_val_before_order:" + m5_min_val_before_order.ToString() + ",m5_max_val_before_order:" + m5_max_val_before_order.ToString();
                Print(log_str);
                WriteTextToFile(log_str);

                if (last_order_type != order_type && last_order_type != 0)
                {
                    //如果前后两单方向不一致，清空m5最低值或最高值
                    if (last_order_type == 1)
                    {
                        m5_max_val_before_order = 50;
                    }
                    else if (last_order_type == 2)
                    {
                        m5_min_val_before_order = 50;
                    }
                }

                order_timestamp = GetCurrTimeStamp();
                var position = result.Position;
                log_str = "下单成功，入场价格为：" + position.EntryPrice.ToString();
                Print(log_str);
                WriteTextToFile(log_str);
                is_set_ping_cang_timestamp_when_has_not_order = false;
                has_order = true;
                last_order_type = order_type;
                price_when_order = position.EntryPrice;
                m5_val_when_order = curr_m5_val;
            }
        }

        //获取CCI指标的趋势
        private int[] GetCciQuShi(string[] data)
        {
            int[] res = new int[] 
            {
                0,
                0
            };

            int num_total = 0;
            int num_equal = 0;
            int num_greater = 0;
            int num_less = 0;
            string[] item_of_late = data[data.Length - 1].Split('|');

            for (var i = data.Length - 1; i > 0; i--)
            {
                num_total++;

                string[] tmp_str_arr_next = data[i].Split('|');
                string[] tmp_str_arr_pre = data[i - 1].Split('|');

                if (ping_cang_timestamp != 0 && Convert.ToDouble(tmp_str_arr_next[0]) < ping_cang_timestamp)
                {
                    Print("亲，距离上一次平仓才{0}个点，还没有合适的时机，继续观察哦~~~", num_total);
                    return res;
                }

                //只判断10分钟内的趋势
                if (Convert.ToDouble(item_of_late[0]) - Convert.ToDouble(tmp_str_arr_next[0]) > 10 * 60 * 1000)
                {
                    break;
                }

                double tmp_val = Convert.ToDouble(tmp_str_arr_next[1]) - Convert.ToDouble(tmp_str_arr_pre[1]);
                if (tmp_val == 0)
                {
                    num_equal++;
                }
                else if (tmp_val > 0)
                {
                    num_greater++;
                }
                else
                {
                    num_less++;
                }
            }

            if (num_total == 0)
            {
                return res;
            }

            if (curr_m5_val <= -70 && (num_less / num_total - num_greater / num_total > 0.4))
            {
                //CCI指标在-70及以下，且，下降的比例-上升的比例>0.4，则判断为下降趋势，启动策略
                res[0] = 1;
                res[1] = 1;
            }
            else if (curr_m5_val >= 70 && (num_greater / num_total - num_less / num_total > 0.4))
            {
                //CCI指标在70及以上，且，上升的比例-下降的比例>0.4，则判断为上升趋势，启动策略
                res[0] = 1;
                res[1] = 2;
            }

            Print("趋势已判断，共取{0}个点，平{1}点，正{2}点，负{3}点", num_total, num_equal, num_greater, num_less);

            return res;
        }

        private bool IsQiDong(string[] data, int difference, bool ignore_tong_xiang_condition = false)
        {
            for (var i = data.Length - 2; i >= 0; i--)
            {
                string[] tmp_str_arr = data[i].Split('|');
                double tmp_val = Convert.ToDouble(tmp_str_arr[1]);

                if (curr_m5_val <= 50 && curr_m5_val - tmp_val >= difference)
                {
                    //-100以下，呈上行趋势，启动Buy策略
                    return true;
                }
                if (curr_m5_val <= 50 && tmp_val - curr_m5_val >= difference)
                {
                    //-100以下，呈下行趋势，不操作
                    return false;
                }
                if (curr_m5_val >= 50 && tmp_val - curr_m5_val >= difference)
                {
                    //100以上，呈下行趋势，启动Sell策略
                    return true;
                }
                if (curr_m5_val >= 50 && curr_m5_val - tmp_val >= difference)
                {
                    //100以上，呈上行趋势，不操作
                    return false;
                }
            }
            return false;
        }

        //判断是否应该发动策略
        private void IsFaDongCeLue(string[] data_m5, bool ignore_tong_xiang_condition = false)
        {
            if (curr_m5_val <= 25)
            {
                qidong = IsQiDong(data_m5, 5, ignore_tong_xiang_condition);
                if (qidong == true)
                {
                    order_type = 2;
                    take_profit_pips = take_profit_pips_base * 1.6;
                }
            }
            else if (curr_m5_val > 25 && curr_m5_val <= 30)
            {
                qidong = IsQiDong(data_m5, 5, ignore_tong_xiang_condition);
                if (qidong == true)
                {
                    order_type = 2;
                    take_profit_pips = take_profit_pips_base * 1.4;
                }
            }
            else if (curr_m5_val > 30 && curr_m5_val <= 35)
            {
                qidong = IsQiDong(data_m5, 5, ignore_tong_xiang_condition);
                if (qidong == true)
                {
                    order_type = 2;
                    take_profit_pips = take_profit_pips_base * 1.2;
                }
            }
            else if (curr_m5_val > 35 && curr_m5_val <= 40)
            {
                qidong = IsQiDong(data_m5, 5, ignore_tong_xiang_condition);
                if (qidong == true)
                {
                    order_type = 2;
                    take_profit_pips = take_profit_pips_base;
                }
            }
            else if (curr_m5_val > 60 && curr_m5_val <= 65)
            {
                qidong = IsQiDong(data_m5, 5, ignore_tong_xiang_condition);
                if (qidong == true)
                {
                    order_type = 1;
                    take_profit_pips = take_profit_pips_base;
                }
            }
            else if (curr_m5_val > 65 && curr_m5_val <= 70)
            {
                qidong = IsQiDong(data_m5, 5, ignore_tong_xiang_condition);
                if (qidong == true)
                {
                    order_type = 1;
                    take_profit_pips = take_profit_pips_base * 1.2;
                }
            }
            else if (curr_m5_val > 70 && curr_m5_val <= 75)
            {
                qidong = IsQiDong(data_m5, 5, ignore_tong_xiang_condition);
                if (qidong == true)
                {
                    order_type = 1;
                    take_profit_pips = take_profit_pips_base * 1.4;
                }
            }
            else if (curr_m5_val > 75)
            {
                qidong = IsQiDong(data_m5, 5, ignore_tong_xiang_condition);
                if (qidong == true)
                {
                    order_type = 1;
                    take_profit_pips = take_profit_pips_base * 1.6;
                }
            }
        }

        //价格趋势
        private double PriceQuShi(int m5_qu_shi)
        {
            ArrayList al = new ArrayList();
            if (m5_qu_shi == 1)
            {
                al = m5_max_to_now_price_al;
            }
            else if (m5_qu_shi == 2)
            {
                al = m5_min_to_now_price_al;
            }
            if (al.Count < 2)
            {
                return 0;
            }

            int num_total = 0;
            int num_equal = 0;
            int num_greater = 0;
            int num_less = 0;

            for (var i = al.Count - 1; i > 0; i--)
            {
                num_total++;

                double tmp_val = Convert.ToDouble(al[i]) - Convert.ToDouble(al[i - 1]);
                if (tmp_val == 0)
                {
                    num_equal++;
                }
                else if (tmp_val > 0)
                {
                    num_greater++;
                }
                else
                {
                    num_less++;
                }
            }

            if (m5_qu_shi == 1)
            {
                return num_less / num_total;
            }
            else
            {
                return num_greater / num_total;
            }

        }

        //将日志写入文件
        private bool WriteTextToFile(string txt, int index = 1)
        {
            try
            {
                if (index == 0 && File.Exists(path_log))
                {
                    File.Delete(path_log);
                }
                FileStream fs = new FileStream(path_log, FileMode.Append);
                using (StreamWriter sw = new StreamWriter(fs))
                {
                    int year = DateTime.Now.Year;
                    int month = DateTime.Now.Month;
                    int day = DateTime.Now.Day;
                    int hour = DateTime.Now.Hour;
                    int minute = DateTime.Now.Minute;
                    int second = DateTime.Now.Second;
                    string datetime = year.ToString() + "-" + month.ToString() + "-" + day.ToString() + " " + hour.ToString() + ":" + minute.ToString() + ":" + second.ToString();
                    sw.WriteLine(datetime + "|" + txt);
                    return true;
                }
            } catch (Exception e)
            {
                Print("An error occurred:{0}", e.Message);
                return false;
            }
        }

    }
}
