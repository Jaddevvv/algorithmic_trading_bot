using cAlgo.API;
using cAlgo.API.Indicators;
using System;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None, AddIndicators = true)]
    public class SupertrendTEST : Robot
    {

        private Supertrend _supertrend;

        private int _tradesToday = 0; // Track number of trades per day
        private bool _hasWonToday = false; // Track if a winning trade has occurred
        private DateTime _lastTradeDate; // Track the date of the last trade
        
        [Parameter("Take Profit (Pips)", DefaultValue = 100000, MaxValue = 100000, MinValue = 100000, Step = 1)]
        public double TakeProfitInPips { get; set; }

        [Parameter("Label", DefaultValue = "SupertrendTEST")]
        public string Label { get; set; }

        [Parameter("Periods", DefaultValue = 10, Group = "Supertrend", MinValue = 1)]
        public int Periods { get; set; }

        [Parameter("Multiplier", DefaultValue = 5.7, Group = "Supertrend")]
        public double Multiplier { get; set; }
        
        [Parameter("Risk Amount ($)", DefaultValue = 1000)]
        public double RiskAmount { get; set; }

        public Position[] BotPositions
        {
            get
            {
                return Positions.FindAll(Label);
            }
        }

        protected override void OnStart()
        {
            _supertrend = Indicators.Supertrend(Periods, Multiplier);
        }
        
        //Works Somehow for NDXUSD, because pipValue is equal to 1
        private double CalculateLotSize(double riskAmount, double stopLossInPips, double pipValue)
        {
            double lotSize = riskAmount / (stopLossInPips * pipValue);
            
            lotSize = Math.Round(lotSize,0);

            return lotSize;
        }
        
        // Method to check if current time is within 1:30 PM to 6:30 PM UTC (corresponding to 3:30 PM to 8:30 PM UTC+2)
        private bool IsWithinTradingHours()
        {
            var currentUtcTime = Server.Time; // Server time is in UTC
        
            // Get the Eastern Time zone, which handles daylight saving time
            TimeZoneInfo easternZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        
            // Convert UTC time to Eastern Time
            var currentEasternTime = TimeZoneInfo.ConvertTimeFromUtc(currentUtcTime, easternZone);
        
            // Define trading start and end times in Eastern Time
            var startTradingTime = new TimeSpan(9, 30, 0); // 9:30 AM Eastern Time
            var endTradingTime = new TimeSpan(15, 30, 0);  // 3:30 PM Eastern Time
        
            // Check if current Eastern Time is within trading hours
            return currentEasternTime.TimeOfDay >= startTradingTime && currentEasternTime.TimeOfDay <= endTradingTime;
        }
        
        // Method to reset the trade counter if it's a new day
        private void CheckAndResetDailyTradeCount()
        {
            if (_lastTradeDate.Date != Server.Time.Date)
            {
                _tradesToday = 0;
                _hasWonToday = false; // Reset the win flag for the new day
            }
        }

        protected override void OnBarClosed()
        {
            //No Logic if it is not during my timeframe 69% profit if there's only this here.
            //if (!IsWithinTradingHours()) return;
            
            
            // Check if there's already an open position; if so, skip opening a new one && check if you need to close position
            if (BotPositions.Length > 0)
            {
                ClosePositions();
                return;
            }
            
            if (!IsWithinTradingHours()) return;
            

            // Stop trading for the day if max 2 trades have been made or 1 win has occurred
            if (_tradesToday >= 2 || _hasWonToday)
                {
                // Reset trades if it's a new day
                CheckAndResetDailyTradeCount();
                return;
                }
               
            // Check for Supertrend bullish flip and open a buy trade
            if (double.IsNaN(_supertrend.UpTrend.Last(0)) == false && double.IsNaN(_supertrend.UpTrend.Last(1)))
            {
                double stopLossPrice = _supertrend.UpTrend.Last(0);
                double entryPrice = Bars.ClosePrices.Last(0);
                double stopLossInPips = (entryPrice - stopLossPrice) / Symbol.PipSize; // Calculate stop-loss in pips
                
                double lotSize = CalculateLotSize(RiskAmount,stopLossInPips,Symbol.PipValue);
                Print("lotSize : ",lotSize);

                ExecuteMarketOrder(TradeType.Buy, SymbolName, lotSize, Label, stopLossInPips, TakeProfitInPips);
                _tradesToday++;
                _lastTradeDate = Server.Time;
            }
            // Check for Supertrend bearish flip and open a sell trade
            else if (double.IsNaN(_supertrend.DownTrend.Last(0)) == false && double.IsNaN(_supertrend.DownTrend.Last(1)))
            {
                double stopLossPrice = _supertrend.DownTrend.Last(0);
                double entryPrice = Bars.ClosePrices.Last(0);
                double stopLossInPips = (stopLossPrice - entryPrice) / Symbol.PipSize; // Calculate stop-loss in pips
                
                //double pipValue = CalculatePipValuePerLot();
                double lotSize = CalculateLotSize(RiskAmount,stopLossInPips,Symbol.PipValue);
                //double lotSize = 8;
                //Print("pipValue : ", pipValue);
                Print("lotSize : ",lotSize);
                
                ExecuteMarketOrder(TradeType.Sell, SymbolName, lotSize, Label, stopLossInPips, TakeProfitInPips);
                _tradesToday++;
                _lastTradeDate = Server.Time;
            }
        }
        
        private void ClosePositions()
        {
            double superUpTrendPrice = _supertrend.UpTrend.Last(0);
            double superDownTrendPrice = _supertrend.DownTrend.Last(0);
            double currentClosedPrice = Bars.ClosePrices.Last(0);
            
            //Print("LONG : Uptrend Price" , superUpTrendPrice);
            //Print("SHORT : Downtrend Price" , superDownTrendPrice);
            //Print("LONG SHORT : current price" , currentClosedPrice);
            
            foreach (var position in BotPositions)
            {
                if (position.TradeType == TradeType.Sell && double.IsNaN(superDownTrendPrice))
                {
                ClosePosition(position);
                if (position.NetProfit > 0)
                    {
                        _hasWonToday = true; // Set the win flag if this trade was a winner
                        return;
                    }
                if ((_tradesToday >= 2) || !IsWithinTradingHours()) return;
                
                double stopLossInPips = (currentClosedPrice - superUpTrendPrice) / Symbol.PipSize; // Calculate stop-loss in pips
                double lotSize = CalculateLotSize(RiskAmount,stopLossInPips,Symbol.PipValue);

                
                ExecuteMarketOrder(TradeType.Buy, SymbolName, lotSize, Label, stopLossInPips, TakeProfitInPips);
                _tradesToday++;
                _lastTradeDate = Server.Time;
                
                }

                else if (position.TradeType == TradeType.Buy && double.IsNaN(superUpTrendPrice))
                {
                ClosePosition(position);
                if (position.NetProfit > 0)
                    {
                        _hasWonToday = true; // Set the win flag if this trade was a winner
                        return;
                    }   
                if ((_tradesToday >= 2) || !IsWithinTradingHours()) return;
                
                double stopLossInPips = (superDownTrendPrice - currentClosedPrice) / Symbol.PipSize; // Calculate stop-loss in pips
                double lotSize = CalculateLotSize(RiskAmount,stopLossInPips,Symbol.PipValue);

                ExecuteMarketOrder(TradeType.Sell, SymbolName, lotSize, Label, stopLossInPips, TakeProfitInPips);
                _tradesToday++;
                _lastTradeDate = Server.Time;
                }
            }
        }
    }
}
