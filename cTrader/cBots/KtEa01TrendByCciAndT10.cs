using System;
using System.IO;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;

//趋势策略，结合CCI和t10

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.FileSystem)]
    public class KtEa01TrendByCciAndT10 : Robot
    {
        [Parameter(DefaultValue = 0.0)]
        public double Parameter { get; set; }

        //策略是否启动
        private bool qidong = false;
        //下单操作类型，1:Sell,2:Buy,0:未知
        private int order_type = 0;
        //订单标签
        public string order_label = "first_order";
        //差值（衡量上升或下降趋势的标识）
        public int difference = 75;
        //启动EA判断是否执行策略的临界值的绝对值
        public int lin_jie_val = 70;
        //下单数量
        public int order_volume = 100000;
        //止损点
        public int stop_loss_pips = 6;
        //下单时间戳
        private long order_timestamp = 0;
        //m5数据CCI指标下单后最低值
        private double after_order_lowest_val = 0;
        //m5数据CCI指标下单后最高值
        private double after_order_highest_val = 0;
        //平仓后m5数据CCI指标最低值
        private double after_ping_cang_lowest_val = 0;
        //平仓后m5数据CCI指标最高值
        private double after_ping_cang_highest_val = 0;
        //是否已经平仓
        private bool has_ping_cang = false;
        //平仓时的时间戳
        private long ping_cang_timestamp = 0;
        //是否在没找到订单时设置了平仓时间戳
        private bool is_set_ping_cang_timestamp_when_has_not_order = false;
        //是否下过订单，只要下过一单都算;
        private bool has_order = false;
        //m5数据保存路径
        private string path_m5 = "C:\\csharp\\m5.txt";
        //t10数据保存路径
        private string path_t10 = "C:\\csharp\\t10.txt";
        //当前m5值
        private double curr_m5_val;

        protected override void OnStart()
        {
            Print("EA启动...");
        }

        protected override void OnTick()
        {
            Check();
        }

        protected override void OnStop()
        {
            Print("EA停止...");
        }

        private void Check()
        {
            string[] data_m5 = getLatestData(path_m5, 1000);
            string[] data_t10 = getLatestData(path_t10);

            if (data_m5.Length == 0)
            {
                Print("m5数据正在写入，稍等~~~");
                return;
            }

            if (data_t10.Length == 0)
            {
                Print("t10数据正在写入，稍等~~~");
                return;
            }

            Position position;
            curr_m5_val = GetCurrM5Val(data_m5);

            //已经平仓的情况，先检测是否有反转的迹象
            if (has_ping_cang == true)
            {
                if (order_type == 1)
                {
                    if (after_ping_cang_highest_val - curr_m5_val >= difference)
                    {
                        //Sell方向达到反转
                        Print("m5数据发现反转做Sell时机，观察t10数据，是否达到做Sell条件");
                        if (Convert.ToDouble(data_t10[0]) == -1)
                        {
                            Print("哇哦，达到反转做Sell单条件了，即将下Sell单");
                            PlaceOrder(TradeType.Sell);
                        }
                        else
                        {
                            Print("反转：哎呀，t10还不符合要求，再等等呢~~~");
                        }
                        return;
                    }
                }
                else if (order_type == 2)
                {
                    if (curr_m5_val - after_ping_cang_lowest_val >= difference)
                    {
                        Print("m5数据发现反转做Buy时机，观察t10数据，是否达到做Buy条件");
                        if (Convert.ToDouble(data_t10[0]) == 1)
                        {
                            Print("哇哦，反转达到做Buy单条件了，即将下Buy单");
                            PlaceOrder(TradeType.Buy);
                        }
                        else
                        {
                            Print("反转：哎呀，t10还不符合要求，再等等呢~~~");
                        }
                        return;
                    }
                }
            }

            //查找订单，若存在，则判断是否达到平仓条件
            position = Positions.Find(order_label);
            if (position != null)
            {
                Print("找到了已创建的订单，当前毛利润为：{0}", position.GrossProfit);

                //判断是否需要平仓
                bool isNeedPingCang = false;

                if (order_type == 1)
                {
                    //Sell单的情况
                    if (curr_m5_val < after_order_lowest_val)
                    {
                        after_order_lowest_val = curr_m5_val;
                    }
                    if (after_order_lowest_val > -175)
                    {
                        //在-175以上，不做任何操作，等其自动止损离场
                    }
                    else if (after_order_lowest_val <= -175)
                    {
                        //在-175及以下，判断当前值和最低值的差值，若差值>=75，则平仓
                        if (curr_m5_val - after_order_lowest_val >= difference)
                        {
                            isNeedPingCang = true;
                        }
                    }
                }
                else if (order_type == 2)
                {
                    //Buy单的情况
                    if (curr_m5_val > after_order_highest_val)
                    {
                        after_order_highest_val = curr_m5_val;
                    }
                    if (after_order_highest_val < 175)
                    {
                        //在175以下，不做任何操作，等其自动止损离场
                    }
                    else if (after_order_highest_val >= 175)
                    {
                        //在175及以上，判断最高值和当前值的差值，若差值>=75，则平仓
                        if (after_order_highest_val - curr_m5_val >= difference)
                        {
                            isNeedPingCang = true;
                        }
                    }
                }

                if (isNeedPingCang == true)
                {
                    double gross_profit = position.GrossProfit;
                    ClosePosition(position);
                    has_ping_cang = true;
                    if (order_type == 1)
                    {
                        after_ping_cang_highest_val = curr_m5_val;
                    }
                    else if (order_type == 2)
                    {
                        after_ping_cang_lowest_val = curr_m5_val;
                    }
                    Print("平仓成功,该订单的毛利润为{0}", gross_profit);
                    return;
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


            //重置启动值为false
            qidong = false;

            //判断当前m5的CCI指标值值是否>=70或<=-70
            int tmp_num = 0;
            for (var i = data_m5.Length - 2; i >= 0; i--)
            {
                tmp_num++;
                string[] tmp_str_arr = data_m5[i].Split('|');
                double tmp_val = Convert.ToDouble(tmp_str_arr[1]);

                if (ping_cang_timestamp != 0 && Convert.ToDouble(tmp_str_arr[0]) < ping_cang_timestamp)
                {
                    Print("亲，距离上一次平仓才{0}个点，还没有合适的时机，继续观察哦~~~", tmp_num);
                    break;
                }

                if (curr_m5_val <= -lin_jie_val && tmp_val - curr_m5_val >= difference)
                {
                    //-lin_jie_val以下，呈下行趋势，启动Sell策略
                    qidong = true;
                    order_type = 1;
                    break;
                }
                if (curr_m5_val <= -lin_jie_val && curr_m5_val - tmp_val >= difference)
                {
                    //-lin_jie_val以下，呈上行趋势，不操作
                    qidong = false;
                    break;
                }
                if (curr_m5_val >= lin_jie_val && curr_m5_val - tmp_val >= difference)
                {
                    //lin_jie_val以上，呈上行趋势，启动Buy策略
                    qidong = true;
                    order_type = 2;
                    break;
                }
                if (curr_m5_val >= lin_jie_val && tmp_val - curr_m5_val >= difference)
                {
                    //lin_jie_val以上，呈下行趋势，不操作
                    qidong = false;
                    break;
                }
            }

            if (qidong == false)
            {
                Print("目前没有较好的时机，继续观察,after_ping_cang_highest_val:{0},after_ping_cang_lowest_val:{1},curr_m5_val:{2}", after_ping_cang_highest_val, after_ping_cang_lowest_val, curr_m5_val);
                return;
            }

            if (order_type == 1)
            {
                //做Sell单的情况
                Print("m5数据发现做Sell时机，观察t10数据，是否达到做Sell条件");
                if (Convert.ToDouble(data_t10[0]) == -1)
                {
                    Print("哇哦，达到做Sell单条件了，即将下Sell单");
                    PlaceOrder(TradeType.Sell);
                }
                else
                {
                    Print("哎呀，t10还不符合要求，再等等呢~~~");
                }
            }
            else if (order_type == 2)
            {
                //做Buy单的情况
                Print("m5数据发现做Buy时机，观察t10数据，是否达到做Buy条件");
                if (Convert.ToDouble(data_t10[0]) == 1)
                {
                    Print("哇哦，达到做Buy单条件了，即将下Buy单");
                    PlaceOrder(TradeType.Buy);
                }
                else
                {
                    Print("哎呀，t10还不符合要求，再等等呢~~~");
                }
            }
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
                if (data.Length < num_of_val)
                {
                    string[] tmp_str_arr = data[data.Length - 1].Split('|');
                    return Convert.ToDouble(tmp_str_arr[1]);
                }
                for (var i = data.Length - 1; i >= data.Length - num_of_val; i--)
                {
                    string[] tmp_str_arr = data[i].Split('|');
                    sum += Convert.ToDouble(tmp_str_arr[1]);
                }
                return (double)sum / num_of_val;
            }
        }

        //下单操作
        private void PlaceOrder(TradeType tt)
        {
            var result = ExecuteMarketOrder(tt, Symbol, order_volume, order_label, stop_loss_pips, null, null, "test", true);
            if (result.IsSuccessful)
            {
                order_timestamp = GetCurrTimeStamp();
                Position position = result.Position;
                Print("下单成功，入场价格为： {0}", position.EntryPrice);
                has_ping_cang = false;
                is_set_ping_cang_timestamp_when_has_not_order = false;
                has_order = true;
                if (tt == TradeType.Sell)
                {
                    after_order_lowest_val = curr_m5_val;
                }
                else
                {
                    after_order_highest_val = curr_m5_val;
                }
            }
        }

        //获取当前时间戳
        private long GetCurrTimeStamp()
        {
            long currentTicks = DateTime.Now.Ticks;
            DateTime dtFrom = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            long currentMillis = (currentTicks - dtFrom.Ticks) / 10000 - (8 * 3600 * 1000);
            return currentMillis;
        }

    }
}
