//策略05
//如果没有达到止盈点出场，则加倍下单数量（可配置反向或同向下单）

using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class KtEa05DoubleVolumeIfJudgmentError : Robot
    {
        [Parameter(DefaultValue = 0.1, MinValue = 0.01)]
        public double FirstLotNumberOfHands { get; set; }

        [Parameter(DefaultValue = 20)]
        public int TakeProfitPips { get; set; }

        [Parameter(DefaultValue = 20)]
        public int StopLossPips { get; set; }

        [Parameter(DefaultValue = false)]
        public bool StopLossPipsEqualTakeProfitPips { get; set; }

        [Parameter(DefaultValue = false)]
        public bool SameDirection { get; set; }

        [Parameter(DefaultValue = true)]
        public bool HasTrailingStop { get; set; }

        [Parameter(DefaultValue = 5, MinValue = 2, MaxValue = 20)]
        public int MaxPower { get; set; }


        private string order_label = "my_order";
        private double curr_gross_profit = 0;
        private double curr_lot;
        private int last_order_type = -1;
        private double last_order_balance = -1;
        private double take_profit_target = 0;

        protected override void OnStart()
        {
            curr_lot = FirstLotNumberOfHands;
        }

        protected override void OnBar()
        {
            Position position = Positions.Find(order_label);
            if (position == null)
            {
                if (last_order_balance == -1)
                {
                    //首单
                    curr_lot = FirstLotNumberOfHands;
                    SendFirstOrder(curr_lot);
                }
                else if (Account.Balance - last_order_balance >= take_profit_target)
                {
                    //止盈出局，然后正常下单，加倍的重置
                    Print("止盈出局");
                    curr_lot = FirstLotNumberOfHands;
                    SendFirstOrder(curr_lot);
                }
                else if (Account.Balance - last_order_balance < take_profit_target)
                {
                    //止损出局
                    Print("止损出局");

                    if (curr_lot / FirstLotNumberOfHands < Math.Pow(2, MaxPower) - 1)
                        curr_lot = curr_lot * 2;

                    if (SameDirection)
                    {
                        //同向
                        if (last_order_type == 0)
                        {
                            SendOrder(TradeType.Buy, curr_lot);
                        }
                        else if (last_order_type == 1)
                        {
                            SendOrder(TradeType.Sell, curr_lot);
                        }
                    }
                    else
                    {
                        //反向
                        if (last_order_type == 0)
                        {
                            SendOrder(TradeType.Sell, curr_lot);
                            last_order_type = 1;
                        }
                        else if (last_order_type == 1)
                        {
                            SendOrder(TradeType.Buy, curr_lot);
                            last_order_type = 0;
                        }
                    }

                }
                else
                {
                    Print("啥意思，怎么到这里来了，不科学！！！");
                    Print("Account.Balance-last_order_balance:{0}", Account.Balance - last_order_balance);
                    Print("take_profit_target:{0}", take_profit_target);
                }
            }
        }

        protected override void OnStop()
        {

        }

        protected override void OnError(Error CodeOfError)
        {
            if (CodeOfError.Code == ErrorCode.NoMoney)
            {
                Print("资金不足，爆仓了，不能再创建订单，EA停止");
                Stop();
            }
            else if (CodeOfError.Code == ErrorCode.BadVolume)
            {
                Print("无效的下单数量");
            }
            else
            {
                Print("未知错误，错误代码：{0}", CodeOfError.Code);
            }
        }

        private void SendFirstOrder(double OrderVolume)
        {
            int Signal = GetStdIlanSignal();
            if (!(Signal < 0))
                switch (Signal)
                {
                    case 0:
                        SendOrder(TradeType.Buy, OrderVolume);
                        last_order_type = 0;
                        break;
                    case 1:
                        SendOrder(TradeType.Sell, OrderVolume);
                        last_order_type = 1;
                        break;
                }
        }

        private void SendOrder(TradeType tt, double OrderVolume)
        {
            double _slp = StopLossPipsEqualTakeProfitPips ? TakeProfitPips : StopLossPips;
            last_order_balance = Account.Balance;
            ExecuteMarketOrder(tt, Symbol, OrderVolume * 100000, order_label, _slp, TakeProfitPips, null, "", HasTrailingStop);
            take_profit_target = OrderVolume * (TakeProfitPips - 2) * 10;
        }

        private int GetStdIlanSignal()
        {
            int Result = -1;
            int LastBarIndex = MarketSeries.Close.Count - 2;
            int PrevBarIndex = LastBarIndex - 1;

            if (MarketSeries.Close[LastBarIndex] > MarketSeries.Open[LastBarIndex])
                if (MarketSeries.Close[PrevBarIndex] > MarketSeries.Open[PrevBarIndex])
                    Result = 0;
            if (MarketSeries.Close[LastBarIndex] < MarketSeries.Open[LastBarIndex])
                if (MarketSeries.Close[PrevBarIndex] < MarketSeries.Open[PrevBarIndex])
                    Result = 1;
            return Result;
        }

    }
}
