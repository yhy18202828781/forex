//策略04，最简单粗暴
//指标为RSI
//以35和65为基准，这俩值可以设置
//以低于35举例，高于65则反之
//移动止损设置为50点
//若RSI最低值<=35，且当前呈上升趋势（反转达6个点），即做Buy单，接下来，有两种情况
//1、一直呈上升趋势，最高点超过65，然后反转（6个点），认为做Sell单的时机已到，则平仓当前Buy单，然后立马做Sell单
//2、最高点未达到65，且当前RSI值低于做Buy单时RSI的最低值，则认为做反了方向，平仓，等待下一次机会

using System;
using System.IO;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.FileSystem)]
    public class KtEa04SimpleAndRough : Robot
    {
        //Sell触发基准值
        [Parameter("BaseLineSell", DefaultValue = 65)]
        public int BaseLineSell { get; set; }

        //Buy触发基准值
        [Parameter("BaseLineBuy", DefaultValue = 35)]
        public int BaseLineBuy { get; set; }

        //CCI指标期数
        [Parameter("PeriodsCci", DefaultValue = 20)]
        public int PeriodsCci { get; set; }

        //RSI指标期数
        [Parameter("PeriodsRsi", DefaultValue = 14)]
        public int PeriodsRsi { get; set; }

        //止损点
        [Parameter("stop_loss_pips", DefaultValue = 50)]
        public int stop_loss_pips { get; set; }


        //订单
        private Position position;
        //订单标签
        public string order_label = "first_order";
        //订单类型，1:Sell,2:Buy,0:未知
        private int order_type = 0;
        //上一个订单类型，1:Sell,2:Buy,0:未知
        private int last_order_type = 0;
        //下单数量
        public int order_volume = 100000;
        //是否移动（追踪）止损
        private bool has_trailing_stop = true;
        //RSI指标当前值
        private double curr_rsi_val;
        //判断上升或下行趋势的差值
        private int difference_of_trend = 5;
        //RSI指标最低值
        private double min_rsi_val = 50;
        //RSI指标最高值
        private double max_rsi_val = 50;
        //下单时RSI指标最低值
        private double min_rsi_val_when_order = 50;
        //下单时RSI指标最高值
        private double max_rsi_val_when_order = 50;
        //下单时的价格
        private double price_when_order = 0;
        //下单时rsi的值
        private double rsi_val_when_order = 0;
        //日志字符串
        private string log_str = "";
        //日志路径
        private string path_log = "C:\\csharp\\log_kt_ea_01_simple_and_rough.txt";
        //上一刻所在区间，1：50以上，-1：50以下，0：初始
        private int interval_of_last_moment = 0;
        //发现移动止损出局
        private bool has_find_trailing_stop_out = false;
        //当前是否存在订单
        private bool has_order_now = false;


        protected override void OnStart()
        {
            Print("EA启动...");
        }

        protected override void OnBar()
        {
            MainFunc();
        }

        protected override void OnStop()
        {

        }

        //策略主逻辑
        private void MainFunc()
        {
            string[] data_rsi = GetIndicatorsLatestData("rsi", 1000);
            curr_rsi_val = GetCurrRsiVal(data_rsi);

            UpdateMinMaxRsiVal();

            //判断当前有无订单
            position = Positions.Find(order_label);
            if (position != null)
            {
                //存在订单的情况，先看是否下反方向
                if (last_order_type == 1 && curr_rsi_val >= max_rsi_val_when_order && Symbol.Bid > price_when_order)
                {
                    //发现Sell单下反方向，立马平仓
                    log_str = "当前curr_rsi_val>=max_rsi_val_when_order，且价格高于Sell单入场价，平仓（curr_m5_val:" + curr_rsi_val.ToString() + ",rsi_val_when_order:" + rsi_val_when_order.ToString() + ",Symbol.Bid:" + Symbol.Bid.ToString() + ",price_when_order:" + price_when_order.ToString() + ",毛利润为：" + position.GrossProfit.ToString() + "）";
                    Print(log_str);
                    WriteTextToFile(log_str);
                    ClosePosition(position);
                    has_order_now = false;
                    return;
                }
                if (last_order_type == 2 && curr_rsi_val <= min_rsi_val_when_order && Symbol.Ask < price_when_order)
                {
                    //发现Buy单下反方向，立马平仓
                    log_str = "当前curr_rsi_val<=min_rsi_val_when_order，且价格低于Buy单入场价，平仓（curr_m5_val:" + curr_rsi_val.ToString() + ",rsi_val_when_order:" + rsi_val_when_order.ToString() + ",Symbol.Ask:" + Symbol.Ask.ToString() + ",price_when_order:" + price_when_order.ToString() + ",毛利润为：" + position.GrossProfit.ToString() + "）";
                    Print(log_str);
                    WriteTextToFile(log_str);
                    ClosePosition(position);
                    has_order_now = false;
                    return;
                }

                //若没有下反方向，看是否达到反向下单的条件
                if (IsReachMakeOrder(data_rsi) == true)
                {
                    if (order_type != last_order_type)
                    {
                        log_str = "达到反向下单条件，先平仓当前订单，然后再下反向订单，毛利润为：" + position.GrossProfit.ToString();
                        Print(log_str);
                        WriteTextToFile(log_str);
                        ClosePosition(position);
                        has_order_now = false;
                        PlaceOrder();
                    }
                }
            }
            else
            {
                //没有订单的情况
                if (has_find_trailing_stop_out == false && has_order_now == true)
                {
                    //发现移动止损出局的订单
                    has_find_trailing_stop_out = true;
                    has_order_now = false;
                    if (last_order_type == 1)
                    {
                        max_rsi_val = 50;
                    }
                    else if (last_order_type == 2)
                    {
                        min_rsi_val = 50;
                    }
                    UpdateMinMaxRsiVal();
                }
                if (IsReachMakeOrder(data_rsi) == true)
                {
                    PlaceOrder();
                }
            }
        }

        //判断是否需要将最低或最高值更新
        private void UpdateMinMaxRsiVal()
        {
            if (curr_rsi_val < 50 && curr_rsi_val < min_rsi_val)
            {
                //当前值比最小值还小，则将最小值更新为当前值
                min_rsi_val = curr_rsi_val;
                if (min_rsi_val <= BaseLineBuy)
                {
                    interval_of_last_moment = -1;
                }
                //最低值下限25
                min_rsi_val = min_rsi_val < 25 ? 25 : min_rsi_val;
            }
            else if (curr_rsi_val >= 50 && curr_rsi_val > max_rsi_val)
            {
                //当前值比最高值还大，则将最高值更新为当前值
                max_rsi_val = curr_rsi_val;
                if (max_rsi_val >= BaseLineSell)
                {
                    interval_of_last_moment = 1;
                }
                //最高值上限75
                max_rsi_val = max_rsi_val > 75 ? 75 : max_rsi_val;
            }
            log_str = "curr_rsi_val:" + curr_rsi_val.ToString() + ",min_rsi_val:" + min_rsi_val.ToString() + ",max_rsi_val:" + max_rsi_val.ToString();
            Print(log_str);
            WriteTextToFile(log_str);
        }

        //判断是否达到下单条件
        private bool IsReachMakeOrder(string[] data_rsi)
        {
            //int indicators_trend = GetIndicatorsTrend(data_rsi);
            if (interval_of_last_moment == 1 && max_rsi_val >= BaseLineSell && max_rsi_val - curr_rsi_val >= 6)
            {
                //达到做Sell条件
                order_type = 1;
                return true;
            }
            else if (interval_of_last_moment == -1 && min_rsi_val <= BaseLineBuy && curr_rsi_val - min_rsi_val >= 6)
            {
                //达到做Buy条件
                order_type = 2;
                return true;
            }
            return false;
        }

        //下单操作
        private void PlaceOrder()
        {
            var result = ExecuteMarketOrder(order_type == 1 ? TradeType.Sell : TradeType.Buy, Symbol, order_volume, order_label, stop_loss_pips, null, null, "test", has_trailing_stop);
            if (result.IsSuccessful)
            {
                log_str = "下单时，curr_rsi_val:" + curr_rsi_val.ToString() + ",min_rsi_val:" + min_rsi_val.ToString() + ",max_rsi_val:" + max_rsi_val.ToString();
                Print(log_str);
                WriteTextToFile(log_str);

                if (order_type == 1)
                {
                    //Sell单的情况，重置最低点的值
                    min_rsi_val = 50;
                    //将下单时的最高值记录
                    max_rsi_val_when_order = max_rsi_val;
                    interval_of_last_moment = 1;
                }
                else if (order_type == 2)
                {
                    //Buy单的情况，重置最高点的值
                    max_rsi_val = 50;
                    //将下单时的最低值记录
                    min_rsi_val_when_order = min_rsi_val;
                    interval_of_last_moment = -1;
                }

                position = result.Position;
                log_str = "下单成功，入场价格为：" + position.EntryPrice.ToString();
                Print(log_str);
                WriteTextToFile(log_str);
                //is_set_ping_cang_timestamp_when_has_not_order = false;
                has_order_now = true;
                has_find_trailing_stop_out = false;
                last_order_type = order_type;
                price_when_order = position.EntryPrice;
                //m5_val_when_order = curr_m5_val;
                rsi_val_when_order = curr_rsi_val;
            }
        }

        /*
        //获取指标趋势
        private int GetIndicatorsTrend(string[] data)
        {
            if (data.Length < 2)
            {
                return 0;
            }

            for (var i = data.Length - 2; i >= 0; i--)
            {
                string[] tmp_str_arr = data[i].Split('|');
                double tmp_val = Convert.ToDouble(tmp_str_arr[1]);

                if (curr_rsi_val - tmp_val > difference_of_trend)
                {
                    //上行趋势
                    return 2;
                }

                if (curr_rsi_val - tmp_val < difference_of_trend)
                {
                    //下行趋势
                    return 1;
                }
            }

            return 0;
        }
        */




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

        //获取当前rsi值
        private double GetCurrRsiVal(string[] data, int type = 1)
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

        //获取当前时间戳
        private long GetCurrTimeStamp()
        {
            long currentTicks = DateTime.Now.Ticks;
            DateTime dtFrom = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            long currentMillis = (currentTicks - dtFrom.Ticks) / 10000 - (8 * 3600 * 1000);
            return currentMillis;
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
                    return true;
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
