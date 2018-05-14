//#reference: ..\Indicators\HeikenAshi.algo

//完全借鉴别人的EA（网上追随者很多，热度很高）
//根据一系列指标判断涨跌，然后挂单，若挂单盈利，还可追加市价单

using System;
using System.Linq;
using Microsoft.Win32;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;



/*

        Version 2.1
        Developed by afhacker (Ahmad Noman Musleh)
        Email : afhackermubasher@gmail.com

    
*/


namespace cAlgo
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.Registry)]
    public class TMSBot : Robot
    {
        [Parameter("01 自定义指标周期（默认1，最小1，最大2147483647）", DefaultValue = 1, MinValue = 1, MaxValue = 2147483647)]
        public int HeikenPeriod { get; set; }

        [Parameter("02 如果挂单盈利，且满足一定条件，是否同向继续追加市价订单", DefaultValue = false)]
        public bool scaleIn { get; set; }

        [Parameter("03 同向追加订单后，再追加一单，需要递增的赢利点（默认1）\n（比如，只要挂单盈利超过2点，就追加第一单，\n挂单盈利超过4点，增加第二单，超过6点增加第三单）", DefaultValue = 1)]
        public double scalePips { get; set; }

        [Parameter("04 控制追加市价订单下单数量（\n假如第三单，那么下单数量为：资金*风险百分比/3", DefaultValue = false)]
        public bool scalePositionControl { get; set; }

        [Parameter("05 限制追加市价订单的数量", DefaultValue = false)]
        public bool limitScalingIn { get; set; }

        [Parameter("06 追加市价订单的最大数量（默认2）", DefaultValue = 2)]
        public int scaleNumber { get; set; }

        [Parameter("07 挂单止盈或止损出局时，同时关闭因该挂单产生的市价单", DefaultValue = false)]
        public bool motherClose { get; set; }


        [Parameter("08 订单创建时，止损基于AverageTrueRange指标（\n否的话基于自定义指标）", DefaultValue = true)]
        public bool atrSl { get; set; }

        [Parameter("09 AverageTrueRange指标周期（默认20）", DefaultValue = 20)]
        public int atrPeriod { get; set; }

        [Parameter("10 创建订单时，基于ATR指标止损的倍数（默认3）", DefaultValue = 3)]
        public int atrMultiplier { get; set; }


        [Parameter("11 基于MovingAverage指标（价格和信号）（TDI）平仓所有订单", DefaultValue = false)]
        public bool tdiBasedExit { get; set; }

        [Parameter("12 设置止盈", DefaultValue = false)]
        public bool rrBasedExit { get; set; }

        [Parameter("13 止盈点对止损点的倍数（默认2）", DefaultValue = 2)]
        public double rrAmount { get; set; }

        [Parameter("14 基于自定义指标平仓所有订单", DefaultValue = false)]
        public bool candleBasedExit { get; set; }

        [Parameter("15 不管盈利或亏损都平仓所有订单（上一条件为true的情况，\n且自定义指标出现反向信号）", DefaultValue = false)]
        public bool sameExit { get; set; }

        [Parameter("16 追踪止损", DefaultValue = false)]
        public bool slTrail { get; set; }

        [Parameter("17 基于点数（修改止损点）", DefaultValue = false)]
        public bool pipBased { get; set; }

        [Parameter("18 盈利超过多少点时修改止损点（基于点数）（默认1\n），上一条件为true时）（\n以BUY为例，还有种情况， 假如当前止损点已经高于入场点，\n则盈利超过该值+（止损点-入场点））时修改止损", DefaultValue = 1)]
        public double profitSl { get; set; }

        [Parameter("19 达到修改止损点条件时，修改的止损点较上次移动的点数\n（默认0.5）", DefaultValue = 0.5)]
        public double moveSl { get; set; }

        [Parameter("20 基于RR（修改止损点）", DefaultValue = false)]
        public bool rrBased { get; set; }

        [Parameter("21 当盈利达到特定值的beRSl倍，修改止损点（特定值：以Buy\n为例，入场价-原止损点的点差）（默认2）", DefaultValue = 2)]
        public double beRSl { get; set; }

        [Parameter("22 时间过滤器", DefaultValue = false)]
        public bool timeFilter { get; set; }

        [Parameter("23 策略开始时刻（小时）（默认4）", DefaultValue = 4, MinValue = 0, MaxValue = 23)]
        public int startHour { get; set; }

        [Parameter("24 策略结束时刻（小时）（默认13）", DefaultValue = 13, MinValue = 0, MaxValue = 23)]
        public int endHour { get; set; }

        [Parameter("25 避开星期五", DefaultValue = false)]
        public bool avoidFriday { get; set; }

        [Parameter("26 挂单点数（默认1）（已经出现Buy或Sell信号，\n超过自定义指标最后高值x点挂Buy,Sell反之）", DefaultValue = 1, MinValue = 0.1, MaxValue = 20)]
        public double orderDistance { get; set; }

        [Parameter("27 风险百分比（默认0.5）（如：最多将10%的钱用于下单）%", DefaultValue = 0.5, MinValue = 0.1, MaxValue = 10.0)]
        public double riskPercentage { get; set; }

        [Parameter("28 判断涨跌信号往前推的柱子数量（自定义指标）（默认3）", DefaultValue = 3)]
        public int haColor { get; set; }



        // TDI parameters
        [Parameter("29 RSI指标使用的Source类型（默认Close）")]
        public DataSeries Source { get; set; }

        [Parameter("30 RSI指标周期（默认13）", DefaultValue = 13)]
        public int RsiPeriod { get; set; }

        [Parameter("31 RSI指标周期（价格）（默认2）", DefaultValue = 2)]
        public int PricePeriod { get; set; }

        [Parameter("32 MovingAverage指标周期（信号）（默认7）", DefaultValue = 7)]
        public int SignalPeriod { get; set; }

        [Parameter("33 MovingAverage指标信号和价格线距离（TDI）\n（判断是否达到挂单的条件）（默认1）", DefaultValue = 1)]
        public double spDistance { get; set; }

        [Parameter("34 判断MovingAverage指标（信号和价格）（TDI）\n是否符合挂单条件往前推的柱子数量（默认3）", DefaultValue = 3)]
        public int barsDistaceCheck { get; set; }

        [Parameter("35 BollingerBands指标周期（默认34）", DefaultValue = 34)]
        public int Volatility { get; set; }

        [Parameter("36 BollingerBands指标标准偏差（默认2）", DefaultValue = 2)]
        public int StDev { get; set; }

        [Parameter("37 MovingAverage指标的MovingAverageType参数（价格）\n（默认Simple）", DefaultValue = MovingAverageType.Simple)]
        public MovingAverageType PriceMaType { get; set; }

        [Parameter("38 MovingAverage指标的MovingAverageType参数（信号）\n（默认Simple）", DefaultValue = MovingAverageType.Simple)]
        public MovingAverageType SignalMaType { get; set; }

        [Parameter("39 参考BollingerBands指标", DefaultValue = false)]
        public bool trendFilter { get; set; }

        [Parameter("40 参考超买超卖值", DefaultValue = false)]
        public bool overBSFilter { get; set; }

        [Parameter("41 超买临界值（默认80）", DefaultValue = 80)]
        public double overBought { get; set; }

        [Parameter("42 超卖临界值（默认20）", DefaultValue = 20)]
        public double overSold { get; set; }

        [Parameter("43 参考动量", DefaultValue = false)]
        public bool mommentumFilter { get; set; }

        [Parameter("44 做买力量最小值（默认50）", DefaultValue = 50)]
        public double buyMommentum { get; set; }

        [Parameter("45 做卖力量最大值（默认50）", DefaultValue = 50)]
        public double sellMommentum { get; set; }

        [Parameter("46 参考StochasticOscillator指标", DefaultValue = true)]
        public bool stochasticFilter { get; set; }

        [Parameter("47 StochasticOscillator指标MovingAverageType参数（\n默认Simple）", DefaultValue = MovingAverageType.Simple)]
        public MovingAverageType stMaType { get; set; }

        [Parameter("48 StochasticOscillator指标K线周期（默认20）", DefaultValue = 20)]
        public int kPeriods { get; set; }

        [Parameter("49 StochasticOscillator指标K线Slowing（默认3）", DefaultValue = 3)]
        public int kSlowing { get; set; }

        [Parameter("50 StochasticOscillator指标D线周期（默认20）", DefaultValue = 20)]
        public int dPeriods { get; set; }


        // End of TDI Parameters





        //表明价格波动程度
        private AverageTrueRange atr;
        //表明买卖双方的力量对比（超买或超卖）
        private RelativeStrengthIndex _rsi;
        private MovingAverage _price;
        private MovingAverage _signal;
        private BollingerBands _bollingerBands;
        private StochasticOscillator stochastic;
        private HeikenAshi heikenAshi;

        public string labelPerfix;
        public string scalePerfix;

        protected override void OnStart()
        {
            //挂单标签
            labelPerfix = "TMSBot";
            //市价单标签
            scalePerfix = "scale";

            //回测
            if (IsBacktesting)
            {
                int backtestNum = new Random().Next(1, 10000);
                labelPerfix += " BT " + backtestNum;
            }

            if (scaleIn)
            {
                //写注册表
                CreateSubKey();
                if (motherClose)
                    Positions.Closed += OnPositionClose;
            }
            //平均正确范围指标
            atr = Indicators.AverageTrueRange(atrPeriod, MovingAverageType.Simple);
            //相对强度指数指标
            _rsi = Indicators.RelativeStrengthIndex(Source, RsiPeriod);
            //布林带指标
            _bollingerBands = Indicators.BollingerBands(_rsi.Result, Volatility, StDev, MovingAverageType.Simple);
            //移动平均线指标（RSI的平均线）
            _price = Indicators.MovingAverage(_rsi.Result, PricePeriod, PriceMaType);
            _signal = Indicators.MovingAverage(_rsi.Result, SignalPeriod, SignalMaType);
            //随机振荡器指标
            stochastic = Indicators.StochasticOscillator(kPeriods, kSlowing, dPeriods, stMaType);
            //自定义指标
            heikenAshi = Indicators.GetIndicator<HeikenAshi>(HeikenPeriod);

        }

        protected override void OnTick()
        {

            foreach (var position in Positions)
            {
                if (position.SymbolCode == Symbol.Code && position.Label.StartsWith(labelPerfix))
                {
                    if (position.NetProfit > 0)
                    {
                        //挂单盈利的
                        if (slTrail)
                            stopTrailing(position);
                        if (scaleIn)
                            scalingIn(position);
                    }
                }
                else if (position.SymbolCode == Symbol.Code && position.Label.StartsWith(scalePerfix))
                {
                    if (position.NetProfit > 0)
                    {
                        //追加的市价单盈利的
                        if (slTrail)
                            stopTrailing(position);
                    }
                }
            }
        }


        // This method will be called after creation of each new bar instead of ticks
        protected override void OnBar()
        {

            foreach (var order in PendingOrders)
            {
                if (order.Label.StartsWith(labelPerfix) && order.SymbolCode == Symbol.Code)
                {
                    //取消所有挂单
                    CancelPendingOrder(order);
                    Print("取消挂单咯，Id:{0}", order.Id);
                }
            }

            foreach (var position in Positions)
            {
                if ((position.Label.StartsWith(labelPerfix) || position.Label.StartsWith(scalePerfix)) && position.SymbolCode == Symbol.Code)
                {
                    if (position.Label.StartsWith(labelPerfix))
                        //有订单的情况，管理已有订单，判断是否需要平仓
                        tradeManager(position);
                    return;
                }
            }

            //没有订单，才会执行到这里

            // Latest Closed Candle Index
            int index = MarketSeries.Close.Count - 1;

            //自定义指标是否符合
            bool heikenAshiOk = false;
            //看涨信号
            bool bullishSignal = false;
            //看跌信号
            bool bearishSignal = false;
            if (heikenAshi.Close.Last(1) > heikenAshi.Open.Last(1))
            {
                //跌了再涨，即为看涨信号
                for (int i = 2; i <= haColor + 1; i++)
                    if (heikenAshi.Close.Last(i) < heikenAshi.Open.Last(i))
                    {
                        heikenAshiOk = true;
                        bullishSignal = true;
                    }
            }
            else if (heikenAshi.Close.Last(1) < heikenAshi.Open.Last(1))
            {
                //涨了再跌，即为看跌信号
                for (int i = 2; i <= haColor + 1; i++)
                    if (heikenAshi.Close.Last(i) > heikenAshi.Open.Last(i))
                    {
                        heikenAshiOk = true;
                        bearishSignal = true;
                    }
            }

            Print("_price.Result.Last(1):{0},_signal.Result.Last(1):{1}", _price.Result.Last(1), _signal.Result.Last(1));

            // TDI Check
            //_signal和_price比较
            bool tdiOk = false;
            bool spDistanceCheck = false;
            if ((_price.Result.Last(1) > _signal.Result.Last(1)) && (_price.Result.Last(2) < _signal.Result.Last(2)))
            {
                //price线和signal线交叉，price线上涨
                tdiOk = true;
                for (int i = 2; i <= barsDistaceCheck + 1; i++)
                {
                    if (_signal.Result.Last(i) - _price.Result.Last(i) >= spDistance)
                    {
                        //signal线-price线距离大于一定值的情况，认为tdi信号ok
                        spDistanceCheck = true;
                    }
                    else
                    {
                        spDistanceCheck = false;
                        break;
                    }
                }
            }
            else if ((_price.Result.Last(1) < _signal.Result.Last(1)) && (_price.Result.Last(2) > _signal.Result.Last(2)))
            {
                //price线和signal线交叉，price线下跌
                tdiOk = true;
                for (int i = 2; i <= barsDistaceCheck + 1; i++)
                {
                    if (_price.Result.Last(i) - _signal.Result.Last(i) >= spDistance)
                    {
                        //price线-signal线距离大于一定值的情况，认为tdi信号ok
                        spDistanceCheck = true;
                    }
                    else
                    {
                        spDistanceCheck = false;
                        break;
                    }
                }

            }


            // Trend Check
            //布林带指标
            bool trendOk = false;
            if (trendFilter)
            {
                if (bullishSignal && _price.Result.LastValue > _bollingerBands.Main.LastValue && _signal.Result.LastValue > _bollingerBands.Main.LastValue)
                    trendOk = true;
                else if (bearishSignal && _price.Result.LastValue < _bollingerBands.Main.LastValue && _signal.Result.LastValue < _bollingerBands.Main.LastValue)
                    trendOk = true;
            }
            else
                trendOk = true;


            // Over Bought and Over Sold Filter
            //超买超卖
            bool overBSOk = false;
            if (overBSFilter)
            {
                if (bullishSignal && _price.Result.Last(1) < overBought && _signal.Result.Last(1) < overBought)
                    overBSOk = true;
                else if (bearishSignal && _price.Result.Last(1) > overSold && _signal.Result.Last(1) > overSold)
                    overBSOk = true;
            }
            else
                overBSOk = true;

            // Mommentum Filter
            //动量
            bool mommentumFilterOk = false;
            if (mommentumFilter)
            {
                if (bullishSignal && _price.Result.Last(1) > buyMommentum && _signal.Result.Last(1) > buyMommentum)
                    mommentumFilterOk = true;
                else if (bearishSignal && _price.Result.Last(1) < sellMommentum && _signal.Result.Last(1) < sellMommentum)
                    mommentumFilterOk = true;
            }
            else
                mommentumFilterOk = true;

            // Stochastic Filter
            //StochasticOscillator指标过滤
            bool stochasticOk = false;
            if (tdiOk && stochasticFilter)
            {
                if (bullishSignal && stochastic.PercentK.Last(0) > stochastic.PercentD.Last(0))
                    stochasticOk = true;
                else if (bearishSignal && stochastic.PercentK.Last(0) < stochastic.PercentD.Last(0))
                    stochasticOk = true;
            }
            else
                stochasticOk = true;


            // Time Filter
            //时间过滤
            bool isTimeCorrect = false;
            if (timeFilter || avoidFriday)
                isTimeCorrect = timeFilterCheck();
            else
                isTimeCorrect = true;


            // Placing The stop order
            if (heikenAshiOk && tdiOk && isTimeCorrect && stochasticOk && overBSOk && trendOk && mommentumFilterOk && spDistanceCheck)
            {
                // Order Attributes     所有条件都符合挂单的情况
                double stopLoss;
                if (atrSl)
                {
                    stopLoss = Math.Round((atr.Result.LastValue * Math.Pow(10, Symbol.Digits - 1)) * atrMultiplier, 1);
                }
                else
                    stopLoss = (heikenAshi.High.Last(1) - heikenAshi.Low.Last(1)) * Math.Pow(10, Symbol.Digits - 1);

                double? takeProfit = null;
                if (rrBasedExit)
                    takeProfit = stopLoss * rrAmount;

                if (scaleIn)
                {
                    SetValue("TradeLabel", "");
                    SetValue("TradeNumber", "0");
                    SetValue("PipsCount", "0");
                }

                long posVolume = PositionVolume(stopLoss);

                string label = string.Format("{0} {1}", labelPerfix, index);
                //做挂单
                if (bullishSignal)
                {
                    PlaceStopOrder(TradeType.Buy, Symbol, posVolume, heikenAshi.High.Last(1) + (Symbol.PipSize * orderDistance), label, stopLoss, takeProfit);
                }
                else if (bearishSignal)
                {
                    PlaceStopOrder(TradeType.Sell, Symbol, posVolume, heikenAshi.Low.Last(1) - (Symbol.PipSize * orderDistance), label, stopLoss, takeProfit);
                }

            }
        }


        // Manage the trade 管理已有的订单，判断是否需要平仓
        private void tradeManager(Position pos)
        {
            if (pos.TradeType == TradeType.Buy)
            {
                if (tdiBasedExit && _price.Result.Last(1) < _signal.Result.Last(1))
                {
                    //价格线低于信号线，可能是跌的信号
                    CloseAllPositions();
                    Print("MA指标price线低于signal线，可能是跌的信号，平仓所有订单");
                }
                if (candleBasedExit)
                {
                    if (sameExit)
                    {
                        if (heikenAshi.Open.Last(1) > heikenAshi.Close.Last(1))
                        {
                            CloseAllPositions();
                            Print("自定义指标Open高于Close，可能是跌的信号，平仓所有订单，不管盈利或亏损");
                        }
                    }
                    else
                    {
                        if (pos.NetProfit > 0 && heikenAshi.Open.Last(1) > heikenAshi.Close.Last(1))
                        {
                            CloseAllPositions();
                            Print("自定义指标Open高于Close，可能是跌的信号，平仓所有订单，仅盈利才平仓");
                        }
                    }

                }

            }
            else if (pos.TradeType == TradeType.Sell)
            {
                if (tdiBasedExit && _price.Result.Last(1) > _signal.Result.Last(1))
                {
                    CloseAllPositions();
                    Print("MA指标price线高于signal线，可能是涨的信号，平仓所有订单");
                }

                if (candleBasedExit)
                {
                    if (sameExit)
                    {
                        if (heikenAshi.Open.Last(1) < heikenAshi.Close.Last(1))
                        {
                            CloseAllPositions();
                            Print("自定义指标Open低于Close，可能是涨的信号，平仓所有订单，不管盈利或亏损");
                        }
                    }
                    else
                    {
                        if (pos.NetProfit > 0 && heikenAshi.Open.Last(1) < heikenAshi.Close.Last(1))
                        {
                            CloseAllPositions();
                            Print("自定义指标Open低于Close，可能是涨的信号，平仓所有订单，仅盈利才平仓");
                        }
                    }

                }

            }
        }


        // Stop Trailing    修改止损
        private void stopTrailing(Position pos)
        {
            double sl_pip = 0.0;
            if (pos.TradeType == TradeType.Buy)
            {
                if (pipBased)
                {
                    if (pos.StopLoss.HasValue && pos.StopLoss.Value > pos.EntryPrice)
                    {
                        sl_pip = ((pos.StopLoss.Value - pos.EntryPrice) * Math.Pow(10, Symbol.Digits - 1)) + profitSl;
                        if (pos.Pips >= sl_pip)
                        {
                            ModifyPosition(pos, pos.StopLoss.Value + (moveSl * Symbol.PipSize), pos.TakeProfit);
                        }
                    }
                    else
                    {
                        sl_pip = profitSl;
                        if (pos.Pips >= sl_pip)
                        {
                            ModifyPosition(pos, pos.EntryPrice + (moveSl * Symbol.PipSize), pos.TakeProfit);
                        }
                    }
                }


                if (rrBased)
                {
                    sl_pip = (pos.EntryPrice - pos.StopLoss.Value) * Math.Pow(10, Symbol.Digits - 1);

                    if (pos.Pips >= (sl_pip * beRSl) && pos.StopLoss.Value < pos.EntryPrice)
                    {
                        ModifyPosition(pos, pos.EntryPrice + Symbol.PipSize, pos.TakeProfit);
                    }
                }

            }
            else if (pos.TradeType == TradeType.Sell)
            {
                if (pipBased)
                {
                    if (pos.StopLoss.HasValue && pos.StopLoss.Value < pos.EntryPrice)
                    {
                        sl_pip = ((pos.EntryPrice - pos.StopLoss.Value) * Math.Pow(10, Symbol.Digits - 1)) + profitSl;
                        if (pos.Pips > sl_pip)
                        {
                            ModifyPosition(pos, pos.StopLoss.Value - (moveSl * Symbol.PipSize), pos.TakeProfit);
                        }
                    }
                    else
                    {
                        sl_pip = profitSl;
                        if (pos.Pips > sl_pip)
                        {
                            ModifyPosition(pos, pos.EntryPrice - (moveSl * Symbol.PipSize), pos.TakeProfit);
                        }
                    }
                }

                if (rrBased)
                {
                    sl_pip = (pos.StopLoss.Value - pos.EntryPrice) * Math.Pow(10, Symbol.Digits - 1);

                    if (pos.Pips >= (sl_pip * beRSl) && pos.StopLoss.Value > pos.EntryPrice)
                    {
                        ModifyPosition(pos, pos.EntryPrice - Symbol.PipSize, pos.TakeProfit);
                    }
                }
            }
        }


        // Position volume calculator       下单数量计算
        private long PositionVolume(double stopLossInPips)
        {
            double riskPercent = riskPercentage;
            if (scaleIn)
            {
                int TradeNumber = int.Parse(GetFromRegistry("TradeNumber", "0"));
                //第几单（总单数，包含挂单），数量就除以几
                if (scalePositionControl && TradeNumber == 1)
                    riskPercent = riskPercentage / 2;
                else if (scalePositionControl && TradeNumber > 1)
                    riskPercent = riskPercentage / (TradeNumber + 1);
            }


            //每点对应的成本
            double costPerPip = (double)((int)(Symbol.PipValue * 10000000)) / 100;
            //（账户资产*百分比） / （止损点*每点成本），及可下单的交易量（手数）
            double positionSizeForRisk = Math.Round((Account.Balance * riskPercent / 100) / (stopLossInPips * costPerPip), 2);

            if (positionSizeForRisk < 0.01)
                positionSizeForRisk = 0.01;

            //转换为1000的整数倍
            long tmp_volume = Symbol.QuantityToVolume(positionSizeForRisk);
            return tmp_volume / 1000 * 1000;

            //return Symbol.QuantityToVolume(positionSizeForRisk);

        }







        // Checking the opening time of candle      时间过滤（时刻、周五）
        private bool timeFilterCheck()
        {
            bool timeOk = false;
            if (timeFilter && MarketSeries.OpenTime.Last(1).Hour >= startHour && MarketSeries.OpenTime.Last(1).Hour <= endHour)
                timeOk = true;
            else if (!timeFilter)
                timeOk = true;

            bool fridayOk = false;
            if (avoidFriday && MarketSeries.OpenTime.Last(1).DayOfWeek != DayOfWeek.Friday)
                fridayOk = true;
            else if (!avoidFriday)
                fridayOk = true;

            if (timeOk && fridayOk)
                return true;
            else
                return false;
        }



        private void CloseAllPositions()
        {
            foreach (var position in Positions)
            {
                if (position.SymbolCode == Symbol.Code && (position.Label.StartsWith(labelPerfix) || position.Label.StartsWith(scalePerfix)))
                {
                    ClosePosition(position);
                }
            }
        }



        private void scalingIn(Position position)
        {
            string TradeLabel;
            int TradeNumber = int.Parse(GetFromRegistry("TradeNumber", "0"));
            double PipsCount = double.Parse(GetFromRegistry("PipsCount", "0"));
            if (GetFromRegistry("TradeLabel", "") == "")
                TradeLabel = null;
            else
                TradeLabel = GetFromRegistry("TradeLabel", "");

            if (limitScalingIn && TradeNumber >= scaleNumber)
                return;
            double sl;
            if (atrSl)
            {
                sl = Math.Round((atr.Result.LastValue * Math.Pow(10, Symbol.Digits - 1)) * atrMultiplier, 1);
            }
            else
                sl = (heikenAshi.High.Last(1) - heikenAshi.Low.Last(1)) * Math.Pow(10, Symbol.Digits - 1);
            long volume;

            double? tp;
            if (rrBasedExit)
                tp = sl * rrAmount;
            else
                tp = null;



            // If it's then scale in    
            if (position.Pips >= PipsCount && TradeLabel == null)
            {
                //当前暂无市价单，如果挂单的利润点达标，则下第一单市价单
                TradeNumber = 1;
                SetValue("TradeNumber", TradeNumber.ToString());
                volume = PositionVolume(sl);
                TradeLabel = string.Format("{0} {1} {2}", scalePerfix, position.Label, TradeNumber);
                SetValue("TradeLabel", TradeLabel);
                ExecuteMarketOrder(position.TradeType, Symbol, volume, TradeLabel, sl, tp);
                PipsCount += scalePips;
                SetValue("PipsCount", PipsCount.ToString());
            }
            else if (TradeLabel != null)
            {
                var pos = Positions.Find(TradeLabel);
                if (pos != null && pos.Pips >= PipsCount)
                {
                    //已有市价单的情况，如果最后一个市价单的利润点达标，则继续下市价单
                    TradeNumber += 1;
                    SetValue("TradeNumber", TradeNumber.ToString());
                    volume = PositionVolume(sl);
                    TradeLabel = string.Format("{0} {1} {2}", scalePerfix, position.Label, TradeNumber);
                    SetValue("TradeLabel", TradeLabel);
                    ExecuteMarketOrder(position.TradeType, Symbol, volume, TradeLabel, sl, tp);
                    PipsCount += scalePips;
                    SetValue("PipsCount", PipsCount.ToString());
                }
            }
        }



        // Setting, getting and deleting of Registry data
        private void CreateSubKey()
        {
            RegistryKey softwarekey = Registry.CurrentUser.OpenSubKey("Software", true);
            RegistryKey botKey = softwarekey.CreateSubKey(labelPerfix);
            softwarekey.Close();
            botKey.Close();
        }

        private void SetValue(string name, string v)
        {
            RegistryKey botKey = Registry.CurrentUser.OpenSubKey("Software\\" + labelPerfix + "\\", true);
            botKey.SetValue(name, (object)v, RegistryValueKind.String);
            botKey.Close();
        }


        private string GetFromRegistry(string valueName, string defaultValue)
        {
            RegistryKey botKey = Registry.CurrentUser.OpenSubKey("Software\\" + labelPerfix + "\\", false);
            string valueData = (string)botKey.GetValue(valueName, (object)defaultValue);
            botKey.Close();
            return valueData;
        }

        private void DeleteRegistryValue(string name)
        {
            if (GetFromRegistry(name, "0") != "0")
            {
                RegistryKey botKey = Registry.CurrentUser.OpenSubKey("Software\\" + labelPerfix + "\\", true);
                botKey.DeleteValue(name);
                botKey.Close();
            }
        }

        private void DeleteRegistryKey()
        {
            bool noOpenPosition = true;

            if (!IsBacktesting)
            {
                //非回测的情况
                foreach (var position in Positions)
                {
                    if (position.SymbolCode == Symbol.Code && (position.Label.StartsWith(labelPerfix) || position.Label.StartsWith(scalePerfix)))
                    {
                        noOpenPosition = false;
                        break;
                    }

                }
            }

            if (scaleIn && noOpenPosition)
            {
                RegistryKey softwarekey = Registry.CurrentUser.OpenSubKey("Software", true);
                softwarekey.DeleteSubKey(labelPerfix);
                softwarekey.Close();
            }
        }


        //监听仓位关闭
        private void OnPositionClose(PositionClosedEventArgs args)
        {
            var position = args.Position;

            DeleteRegistryValue(position.Label);

            //如果是挂单，且满足一定条件，则关闭因该挂单产生的市价单
            if (scaleIn && motherClose && position.Pips < 0 && position.Label.StartsWith(labelPerfix))
            {
                foreach (var pos in Positions)
                {
                    if (pos.Label.Contains(position.Label))
                    {
                        ClosePosition(pos);
                        Print("挂单被平仓（止损或止盈出场），因该挂单产生的市价单也跟着平仓");
                    }
                }
            }
        }



        protected override void OnStop()
        {
            DeleteRegistryKey();
        }
    }
}
