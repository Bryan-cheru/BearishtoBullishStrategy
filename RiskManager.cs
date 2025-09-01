using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.DrawingTools;
using NinjaTrader.Cbi;
using System.Collections.Generic;
using System.Linq;

namespace NinjaTrader.NinjaScript.Strategies
{
    /// <summary>
    /// Risk management and position sizing functionality for B2B Enhanced strategy
    /// </summary>
    public partial class B2BEnhanced : Strategy
    {
        #region Risk Management Methods

        /// <summary>
        /// Calculates optimal position size using Kelly Criterion and risk management rules
        /// </summary>
        /// <param name="entryPrice">Entry price for the trade</param>
        /// <param name="stopPrice">Stop loss price for the trade</param>
        /// <returns>Position size in contracts/shares</returns>
        private int CalculatePositionSize(double entryPrice, double stopPrice)
        {
            try
            {
                if (Math.Abs(entryPrice - stopPrice).ApproxCompare(0) == 0) return 0;

                // Get the current account value
                double accountValue = Account.Get(AccountItem.CashValue, Currency.UsDollar);

                // Calculate risk percentage
                double riskPercentage = RiskPerTrade;

                // Apply Kelly Criterion if enabled and we have enough trade history
                if (UseKellyCriterion && totalTrades >= MinTradesForKelly)
                {
                    riskPercentage = CalculateKellyRiskPercentage();
                }

                // Reduce risk based on consecutive losses
                if (EnableConsecutiveLossLimit && consecutiveLosses > 0)
                {
                    riskPercentage = ApplyConsecutiveLossReduction(riskPercentage);
                }

                // Calculate risk amount based on account equity
                double riskAmount = accountValue * (riskPercentage / 100.0);

                // Limit maximum risk per trade
                double maxRiskDollars = MaxIntradayLoss * MaxRiskPerTradeMultiplier;
                riskAmount = Math.Min(riskAmount, maxRiskDollars);

                // Calculate risk per unit
                double riskPerUnit = Math.Abs(entryPrice - stopPrice) * Instrument.MasterInstrument.PointValue;
                if (riskPerUnit <= 0) return 0;

                // Calculate position size
                int size = (int)(riskAmount / riskPerUnit);

                // Apply position size limit
                if (EnablePositionSizeLimit)
                {
                    size = Math.Min(size, MaxPositionSize);
                }

                // Ensure minimum size of 1
                size = Math.Max(1, size);

                return size;
            }
            catch (Exception ex)
            {
                Print($"Error in CalculatePositionSize: {ex.Message}");
                return 1; // Default to minimum size on error
            }
        }

        /// <summary>
        /// Calculates risk percentage using Kelly Criterion formula
        /// </summary>
        /// <returns>Risk percentage based on Kelly Criterion</returns>
        private double CalculateKellyRiskPercentage()
        {
            try
            {
                // Kelly formula: f* = (p × b - q) / b
                // Where: f* = kelly fraction, p = win probability, q = loss probability, b = win/loss ratio
                double winProbability = winRate;
                double lossProbability = 1 - winProbability;

                // Ensure we don't divide by zero
                double payoffRatio = avgLoss != 0 ? avgWin / Math.Abs(avgLoss) : 1;

                // Calculate Kelly percentage
                double kellyPercentage = (winProbability * payoffRatio - lossProbability) / payoffRatio;

                // Apply Kelly fraction (conservative approach)
                kellyPercentage = Math.Max(0, kellyPercentage * KellyFraction);

                // Convert to percentage and apply limits
                double riskPercentage = kellyPercentage * 100;
                riskPercentage = Math.Max(MinInitialRisk, Math.Min(MaxInitialRisk, riskPercentage));

                Print($"Kelly Criterion: Win Rate={winProbability:F2}, W/L Ratio={payoffRatio:F2}, Kelly %={riskPercentage:F2}%");

                return riskPercentage;
            }
            catch (Exception ex)
            {
                Print($"Error in CalculateKellyRiskPercentage: {ex.Message}");
                return RiskPerTrade; // Fallback to base risk percentage
            }
        }

        /// <summary>
        /// Applies risk reduction based on consecutive losses
        /// </summary>
        /// <param name="baseRiskPercentage">Base risk percentage to adjust</param>
        /// <returns>Adjusted risk percentage</returns>
        private double ApplyConsecutiveLossReduction(double baseRiskPercentage)
        {
            try
            {
                // Progressively reduce risk for each consecutive loss
                double reductionFactor = Math.Pow(ConsecutiveLossReductionFactor, Math.Min(MaxConsecutiveLossReduction, consecutiveLosses));
                double adjustedRisk = baseRiskPercentage * reductionFactor;

                Print($"Reducing risk after {consecutiveLosses} consecutive losses, adjusted risk: {adjustedRisk:F2}%");

                return adjustedRisk;
            }
            catch (Exception ex)
            {
                Print($"Error in ApplyConsecutiveLossReduction: {ex.Message}");
                return baseRiskPercentage;
            }
        }

        /// <summary>
        /// Validates and adjusts stop price to ensure it's reasonable and within market constraints
        /// </summary>
        /// <param name="proposedStop">Proposed stop loss price</param>
        /// <param name="isLong">True for long positions, false for short positions</param>
        /// <returns>Validated and adjusted stop price</returns>
        private double ValidateStopPrice(double proposedStop, bool isLong)
        {
            try
            {
                double currentPrice = Close[0];
                double highestHigh = High[0];
                double lowestLow = Low[0];

                // Look back a few bars to get a reasonable price range
                for (int i = 1; i < StopValidationLookback; i++)
                {
                    if (CurrentBar >= i)
                    {
                        highestHigh = Math.Max(highestHigh, High[i]);
                        lowestLow = Math.Min(lowestLow, Low[i]);
                    }
                }

                // Calculate acceptable price range (based on recent price action)
                double maxRange = highestHigh - lowestLow;
                double maxStopDistance = Math.Max(cachedATR[0] * MaxStopDistanceATRMultiplier, maxRange);

                // For long positions, stop must be below entry
                if (isLong)
                {
                    // Ensure stop is not too far below current price
                    double minStop = currentPrice - maxStopDistance;
                    double adjustedStop = Math.Max(minStop, proposedStop);

                    // Ensure stop is not above current price (which would trigger immediately)
                    adjustedStop = Math.Min(adjustedStop, currentPrice - TickSize);

                    if (adjustedStop != proposedStop)
                    {
                        Print($"Stop price adjusted: Original={proposedStop:F2}, Adjusted={adjustedStop:F2}");
                    }

                    return adjustedStop;
                }
                // For short positions, stop must be above entry
                else
                {
                    // Ensure stop is not too far above current price
                    double maxStop = currentPrice + maxStopDistance;
                    double adjustedStop = Math.Min(maxStop, proposedStop);

                    // Ensure stop is not below current price (which would trigger immediately)
                    adjustedStop = Math.Max(adjustedStop, currentPrice + TickSize);

                    if (adjustedStop != proposedStop)
                    {
                        Print($"Stop price adjusted: Original={proposedStop:F2}, Adjusted={adjustedStop:F2}");
                    }

                    return adjustedStop;
                }
            }
            catch (Exception ex)
            {
                Print($"Error in ValidateStopPrice: {ex.Message}");
                // Return a reasonable fallback stop price
                return isLong ? Close[0] - (cachedATR[0]) : Close[0] + (cachedATR[0]);
            }
        }

        /// <summary>
        /// Updates Kelly Criterion statistics with latest trade result
        /// </summary>
        /// <param name="tradePnL">Profit/Loss from the completed trade</param>
        private void UpdateKellyStatistics(double tradePnL)
        {
            try
            {
                // Update trade history for Kelly Criterion
                tradeResults.Add(tradePnL);
                totalTrades++;

                if (tradePnL > 0)
                {
                    winningTrades++;
                    totalWinAmount += tradePnL;
                    consecutiveLosses = 0;
                }
                else if (tradePnL < 0)
                {
                    totalLossAmount += Math.Abs(tradePnL);
                    consecutiveLosses++;
                }

                // Update Kelly Criterion parameters
                if (totalTrades > 0)
                {
                    winRate = (double)winningTrades / totalTrades;
                    avgWin = winningTrades > 0 ? totalWinAmount / winningTrades : 0;
                    avgLoss = (totalTrades - winningTrades) > 0 ? totalLossAmount / (totalTrades - winningTrades) : 0;

                    Print($"Updated Kelly metrics: Win Rate={winRate:F2}, Avg Win=${avgWin:F2}, Avg Loss=${avgLoss:F2}");
                }

                // Maintain rolling window of recent trades for more responsive Kelly calculation
                if (tradeResults.Count > MaxKellyTradeHistory)
                {
                    tradeResults.RemoveAt(0);
                    RecalculateKellyFromHistory();
                }
            }
            catch (Exception ex)
            {
                Print($"Error in UpdateKellyStatistics: {ex.Message}");
            }
        }

        /// <summary>
        /// Recalculates Kelly statistics from rolling trade history
        /// </summary>
        private void RecalculateKellyFromHistory()
        {
            try
            {
                if (tradeResults.Count == 0) return;

                var wins = tradeResults.Where(x => x > 0).ToList();
                var losses = tradeResults.Where(x => x < 0).ToList();

                totalTrades = tradeResults.Count;
                winningTrades = wins.Count;
                totalWinAmount = wins.Sum();
                totalLossAmount = Math.Abs(losses.Sum());

                winRate = (double)winningTrades / totalTrades;
                avgWin = winningTrades > 0 ? totalWinAmount / winningTrades : 0;
                avgLoss = losses.Count > 0 ? totalLossAmount / losses.Count : 0;
            }
            catch (Exception ex)
            {
                Print($"Error in RecalculateKellyFromHistory: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if daily loss limit has been reached
        /// </summary>
        /// <returns>True if daily loss limit is reached</returns>
        private bool IsDailyLossLimitReached()
        {
            if (!EnableMaxLoss) return false;

            // Check if daily loss exceeds the limit
            bool limitReached = dailyPnL < 0 && Math.Abs(dailyPnL) >= MaxDailyLoss;

            if (limitReached && !dailyLimitHit)
            {
                dailyLimitHit = true;
                Print($"DAILY LOSS LIMIT REACHED: ${Math.Abs(dailyPnL):F2} exceeds ${MaxDailyLoss:F2}. Trading suspended for today.");
                ExitAllPositions();
            }

            return limitReached;
        }

        /// <summary>
        /// Checks if daily profit target has been reached
        /// </summary>
        /// <returns>True if daily profit target is reached</returns>
        private bool IsDailyProfitTargetReached()
        {
            if (!EnableMaxProfit) return false;

            bool targetReached = dailyPnL >= MaxDailyProfit;

            if (targetReached && !dailyProfitTargetHit)
            {
                dailyProfitTargetHit = true;
                Print($"DAILY PROFIT TARGET REACHED: ${dailyPnL:F2} exceeds ${MaxDailyProfit:F2}. Trading suspended for today.");
                ExitAllPositions();
            }

            return targetReached;
        }

        /// <summary>
        /// Updates drawdown tracking
        /// </summary>
        private void UpdateDrawdownTracking()
        {
            try
            {
                double currentBalance = Account.Get(AccountItem.CashValue, Currency.UsDollar);

                // Update drawdown tracking
                if (currentBalance > peakBalance)
                {
                    peakBalance = currentBalance;
                }
                else
                {
                    double drawdown = 1 - (currentBalance / peakBalance);
                    maxDrawdown = Math.Max(maxDrawdown, drawdown);
                }
            }
            catch (Exception ex)
            {
                Print($"Error in UpdateDrawdownTracking: {ex.Message}");
            }
        }

        #endregion
    }
}