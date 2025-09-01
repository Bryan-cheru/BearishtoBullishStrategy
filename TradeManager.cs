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
    /// Trade entry and management functionality for B2B Enhanced strategy
    /// </summary>
    public partial class B2BEnhanced : Strategy
    {
        #region Trade Entry and Management Methods

        /// <summary>
        /// Generic pattern entry processor that handles both CAHOLD and CBLOHD entries
        /// </summary>
        /// <param name="isLong">True for long (CAHOLD), false for short (CBLOHD)</param>
        /// <param name="patternName">Name of the pattern for logging and order naming</param>
        /// <param name="atr">Current ATR value</param>
        private void ProcessPatternEntry(bool isLong, string patternName, double atr)
        {
            try
            {
                double entryPrice = Close[0];

                // Calculate stop loss - use tighter stops to reduce average loss size
                double stopMultiplier = StopLossATRMultiplier * StopTighteningFactor;
                double maxStopDollar = MaxStopLossDollar / Instrument.MasterInstrument.PointValue;
                double stopSize = Math.Min(atr * stopMultiplier, maxStopDollar);
                
                double rawStopPrice = isLong ? entryPrice - stopSize : entryPrice + stopSize;

                // Validate the stop price
                double stopPrice = ValidateStopPrice(rawStopPrice, isLong);

                // Calculate actual stop distance after validation
                double actualStopDistance = Math.Abs(entryPrice - stopPrice);
                
                // Adjust profit targets based on validated stop distance
                double rewardRiskRatio = Math.Max(MinRewardRiskRatio, ProfitTargetMultiplier);
                double targetPrice = isLong ? 
                    entryPrice + (actualStopDistance * rewardRiskRatio) : 
                    entryPrice - (actualStopDistance * rewardRiskRatio);

                // Calculate position size with Kelly criterion if enabled
                int quantity = CalculatePositionSize(entryPrice, stopPrice);

                if (quantity > 0)
                {
                    // Store for tracking
                    activePattern = patternName;
                    this.entryPrice = entryPrice;
                    currentStopPrice = stopPrice;
                    currentTargetPrice = targetPrice;

                    // Calculate scaled position sizes for multi-stage exits
                    int firstQuantity = Math.Max(1, (int)(quantity * FirstTargetSizeRatio));
                    int secondQuantity = Math.Max(1, (int)(quantity * SecondTargetSizeRatio));
                    int remainingQuantity = quantity - firstQuantity - secondQuantity;
                    remainingQuantity = Math.Max(1, remainingQuantity); // Ensure at least 1

                    // Adjust if total exceeds original quantity
                    if (firstQuantity + secondQuantity + remainingQuantity > quantity)
                    {
                        remainingQuantity = quantity - firstQuantity - secondQuantity;
                        if (remainingQuantity < 1)
                        {
                            secondQuantity--;
                            remainingQuantity = 1;
                        }
                    }

                    // Enter the position
                    if (isLong)
                    {
                        EnterLong(quantity, patternName + "-Entry");
                        SetupLongExitTargets(entryPrice, actualStopDistance, firstQuantity, secondQuantity, remainingQuantity, patternName);
                    }
                    else
                    {
                        EnterShort(quantity, patternName + "-Entry");
                        SetupShortExitTargets(entryPrice, actualStopDistance, firstQuantity, secondQuantity, remainingQuantity, patternName);
                    }

                    // Set stop loss for entire position
                    SetupStopLoss(isLong, quantity, stopPrice, patternName);

                    // Update last evaluation time
                    lastReEvaluationTime = Time[0];

                    double riskAmount = actualStopDistance * Instrument.MasterInstrument.PointValue * quantity;
                    string direction = isLong ? "LONG" : "SHORT";
                    Print($"New {direction} trade at {entryPrice:F2}, Stop: {stopPrice:F2}, Size: {quantity}, Risk: ${riskAmount:F2}, Pattern: {patternName}");
                }
            }
            catch (Exception ex)
            {
                Print($"Error in ProcessPatternEntry: {ex.Message}");
            }
        }

        /// <summary>
        /// Sets up multiple profit targets for long positions
        /// </summary>
        private void SetupLongExitTargets(double entryPrice, double stopDistance, int firstQty, int secondQty, int remainingQty, string patternName)
        {
            try
            {
                // First target at conservative ratio for quick profit
                double firstTarget = entryPrice + (stopDistance * FirstTargetRatio);
                ExitLongLimit(0, true, firstQty, firstTarget, patternName + "-PT1", patternName + "-Entry");

                // Second target at main target ratio
                double secondTarget = entryPrice + (stopDistance * SecondTargetRatio);
                ExitLongLimit(0, true, secondQty, secondTarget, patternName + "-PT2", patternName + "-Entry");

                // Final target for maximum profit
                double finalTarget = entryPrice + (stopDistance * FinalTargetRatio);
                ExitLongLimit(0, true, remainingQty, finalTarget, patternName + "-PT3", patternName + "-Entry");

                Print($"Long targets set - PT1: {firstTarget:F2} ({firstQty}), PT2: {secondTarget:F2} ({secondQty}), PT3: {finalTarget:F2} ({remainingQty})");
            }
            catch (Exception ex)
            {
                Print($"Error in SetupLongExitTargets: {ex.Message}");
            }
        }

        /// <summary>
        /// Sets up multiple profit targets for short positions
        /// </summary>
        private void SetupShortExitTargets(double entryPrice, double stopDistance, int firstQty, int secondQty, int remainingQty, string patternName)
        {
            try
            {
                // First target at conservative ratio for quick profit
                double firstTarget = entryPrice - (stopDistance * FirstTargetRatio);
                ExitShortLimit(0, true, firstQty, firstTarget, patternName + "-PT1", patternName + "-Entry");

                // Second target at main target ratio
                double secondTarget = entryPrice - (stopDistance * SecondTargetRatio);
                ExitShortLimit(0, true, secondQty, secondTarget, patternName + "-PT2", patternName + "-Entry");

                // Final target for maximum profit
                double finalTarget = entryPrice - (stopDistance * FinalTargetRatio);
                ExitShortLimit(0, true, remainingQty, finalTarget, patternName + "-PT3", patternName + "-Entry");

                Print($"Short targets set - PT1: {firstTarget:F2} ({firstQty}), PT2: {secondTarget:F2} ({secondQty}), PT3: {finalTarget:F2} ({remainingQty})");
            }
            catch (Exception ex)
            {
                Print($"Error in SetupShortExitTargets: {ex.Message}");
            }
        }

        /// <summary>
        /// Sets up stop loss for the entire position
        /// </summary>
        private void SetupStopLoss(bool isLong, int quantity, double stopPrice, string patternName)
        {
            try
            {
                if (isLong)
                {
                    ExitLongStopMarket(0, true, quantity, stopPrice, patternName + "-SL", patternName + "-Entry");
                }
                else
                {
                    ExitShortStopMarket(0, true, quantity, stopPrice, patternName + "-SL", patternName + "-Entry");
                }
            }
            catch (Exception ex)
            {
                Print($"Error in SetupStopLoss: {ex.Message}");
            }
        }

        /// <summary>
        /// Processes CAHOLD pattern entry (now uses generic ProcessPatternEntry)
        /// </summary>
        /// <param name="atr">Current ATR value</param>
        private void ProcessCAHOLDEntry(double atr)
        {
            ProcessPatternEntry(true, "CAHOLD", atr);
        }

        /// <summary>
        /// Processes CBLOHD pattern entry (now uses generic ProcessPatternEntry)
        /// </summary>
        /// <param name="atr">Current ATR value</param>
        private void ProcessCBLOHDEntry(double atr)
        {
            ProcessPatternEntry(false, "CBLOHD", atr);
        }

        /// <summary>
        /// Manages active positions with dynamic trailing stops
        /// </summary>
        private void ManageActivePosition()
        {
            try
            {
                if (Position.MarketPosition == MarketPosition.Flat)
                    return;

                double atr = cachedATR[0];
                double pointValue = Instrument.MasterInstrument.PointValue;
                bool isLong = Position.MarketPosition == MarketPosition.Long;

                double currentPrice = Close[0];
                double profitPoints = isLong ? currentPrice - entryPrice : entryPrice - currentPrice;

                // Only update trailing stops if we're in profit
                if (profitPoints <= 0) return;

                double profitAmount = profitPoints * pointValue * Position.Quantity;
                double profitInATR = profitPoints / atr; // Normalize profit to ATR units

                UpdateTrailingStop(isLong, currentPrice, profitInATR, atr, profitAmount);
            }
            catch (Exception ex)
            {
                Print($"Error in ManageActivePosition: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates trailing stop based on profit levels
        /// </summary>
        private void UpdateTrailingStop(bool isLong, double currentPrice, double profitInATR, double atr, double profitAmount)
        {
            try
            {
                double newStop = currentStopPrice;
                string patternPrefix = activePattern;

                // Progressive trailing stop stages
                if (profitInATR >= BreakevenProfitATR && 
                    ((isLong && currentStopPrice < entryPrice) || (!isLong && currentStopPrice > entryPrice)))
                {
                    // Move to breakeven
                    newStop = entryPrice;
                    Print($"Moving stop to breakeven at {newStop:F2}, current profit: ${profitAmount:F2}");
                }
                else if (profitInATR >= TrailingStartATR)
                {
                    // Start trailing based on profit level
                    double trailFactor = GetTrailingFactor(profitInATR);
                    
                    double calculatedStop = isLong ? 
                        currentPrice - (atr * trailFactor) : 
                        currentPrice + (atr * trailFactor);

                    // Only move stop if it's in our favor
                    if ((isLong && calculatedStop > currentStopPrice) || (!isLong && calculatedStop < currentStopPrice))
                    {
                        newStop = calculatedStop;
                        Print($"Updating trailing stop to {newStop:F2} (factor: {trailFactor:F2}), profit: ${profitAmount:F2}");
                    }
                }

                // Apply the stop if it changed and is valid
                if (newStop != currentStopPrice)
                {
                    double validatedStop = ValidateStopPrice(newStop, isLong);
                    
                    if (isLong)
                    {
                        ExitLongStopMarket(0, true, Position.Quantity, validatedStop, patternPrefix + "-Trail", patternPrefix + "-Entry");
                    }
                    else
                    {
                        ExitShortStopMarket(0, true, Position.Quantity, validatedStop, patternPrefix + "-Trail", patternPrefix + "-Entry");
                    }
                    
                    currentStopPrice = validatedStop;
                }
            }
            catch (Exception ex)
            {
                Print($"Error in UpdateTrailingStop: {ex.Message}");
            }
        }

        /// <summary>
        /// Calculates trailing factor based on profit level
        /// </summary>
        /// <param name="profitInATR">Profit normalized to ATR units</param>
        /// <returns>Trailing factor as ATR multiplier</returns>
        private double GetTrailingFactor(double profitInATR)
        {
            // Dynamic trailing factor - tighter as profit increases
            if (profitInATR >= FinalStageTrailATR) return FinalStageTrailFactor;
            if (profitInATR >= Stage3TrailATR) return Stage3TrailFactor;
            if (profitInATR >= Stage2TrailATR) return Stage2TrailFactor;
            return Stage1TrailFactor; // Default trailing factor
        }

        /// <summary>
        /// Re-evaluates position based on changing market conditions
        /// </summary>
        private void ReEvaluatePosition()
        {
            try
            {
                if (Position.MarketPosition == MarketPosition.Flat)
                    return;

                double atr = cachedATR[0];
                bool isLong = Position.MarketPosition == MarketPosition.Long;

                // Check if market conditions have changed significantly
                bool trendChanged = isLong ? !IsInUptrend() : !IsInDowntrend();
                bool volatilityIncreased = atr > cachedATR[VolatilityComparisonBars] * VolatilityIncreaseThreshold;

                // Re-check profit potential
                double currentPrice = Close[0];
                double profitPotential = isLong ?
                    (currentTargetPrice - currentPrice) / (currentPrice - currentStopPrice) :
                    (currentPrice - currentTargetPrice) / (currentStopPrice - currentPrice);

                ExecuteReEvaluationLogic(trendChanged, volatilityIncreased, profitPotential, atr, isLong);
            }
            catch (Exception ex)
            {
                Print($"Error in ReEvaluatePosition: {ex.Message}");
            }
        }

        /// <summary>
        /// Executes re-evaluation logic based on market condition changes
        /// </summary>
        private void ExecuteReEvaluationLogic(bool trendChanged, bool volatilityIncreased, double profitPotential, double atr, bool isLong)
        {
            try
            {
                bool shouldExit = false;
                string exitReason = "";

                if (trendChanged)
                {
                    shouldExit = true;
                    exitReason = "Trend reversal detected";
                }
                else if (volatilityIncreased)
                {
                    HandleVolatilityIncrease(atr, isLong, profitPotential, ref shouldExit, ref exitReason);
                }
                else if (profitPotential < MinProfitPotential)
                {
                    shouldExit = true;
                    exitReason = "Insufficient profit potential remaining";
                }

                // Execute exit if needed
                if (shouldExit)
                {
                    ExitPosition(isLong, exitReason);
                }
            }
            catch (Exception ex)
            {
                Print($"Error in ExecuteReEvaluationLogic: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles position management when volatility increases
        /// </summary>
        private void HandleVolatilityIncrease(double atr, bool isLong, double profitPotential, ref bool shouldExit, ref string exitReason)
        {
            try
            {
                double currentPrice = Close[0];
                
                // Update stop loss to account for higher volatility
                double rawNewStop = isLong ?
                    currentPrice - (atr * ATRMultiplier) :
                    currentPrice + (atr * ATRMultiplier);

                // Validate the new stop price
                double newStop = ValidateStopPrice(rawNewStop, isLong);

                // Only move stop if it's in our favor
                if ((isLong && newStop > currentStopPrice) || (!isLong && newStop < currentStopPrice))
                {
                    string patternPrefix = activePattern;
                    
                    if (isLong)
                    {
                        ExitLongStopMarket(0, true, Position.Quantity, newStop, patternPrefix + "-Vol", patternPrefix + "-Entry");
                    }
                    else
                    {
                        ExitShortStopMarket(0, true, Position.Quantity, newStop, patternPrefix + "-Vol", patternPrefix + "-Entry");
                    }
                    
                    currentStopPrice = newStop;
                    Print($"Updated stop to {newStop:F2} due to increased volatility");
                }
                else if (profitPotential < 1.0)
                {
                    // Exit if volatility increased but reward/risk is below 1:1
                    shouldExit = true;
                    exitReason = "Risk/reward deteriorated with increased volatility";
                }
            }
            catch (Exception ex)
            {
                Print($"Error in HandleVolatilityIncrease: {ex.Message}");
            }
        }

        /// <summary>
        /// Exits position with specified reason
        /// </summary>
        private void ExitPosition(bool isLong, string reason)
        {
            try
            {
                string patternPrefix = activePattern;
                
                if (isLong)
                {
                    ExitLong(0, Position.Quantity, patternPrefix + "-Reeval", patternPrefix + "-Entry");
                }
                else
                {
                    ExitShort(0, Position.Quantity, patternPrefix + "-Reeval", patternPrefix + "-Entry");
                }
                
                Print($"Exiting position during re-evaluation: {reason}");
            }
            catch (Exception ex)
            {
                Print($"Error in ExitPosition: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks for and handles intraday trend reversals
        /// </summary>
        private void CheckForIntradayReversal()
        {
            try
            {
                if (Position.MarketPosition == MarketPosition.Flat || !EnableIntradayReversals)
                    return;

                double atr = cachedATR[0];
                bool isLong = Position.MarketPosition == MarketPosition.Long;

                // Only consider reversals on strong volume and price action
                if (Volume[0] < SMA(Volume, VolumeConfirmationPeriod)[0] * IntradayReversalVolumeThreshold)
                    return;

                bool hasStrongReversal = DetectStrongReversal(isLong, atr);
                bool hasMomentumChange = DetectMomentumChange(isLong);
                bool hasMAAlignmentChange = DetectMAAlignmentChange(isLong);

                // Require multiple confirmations for reversal
                if (hasStrongReversal && hasMomentumChange && hasMAAlignmentChange)
                {
                    ExecuteIntradayReversal(isLong, atr);
                }
            }
            catch (Exception ex)
            {
                Print($"Error in CheckForIntradayReversal: {ex.Message}");
            }
        }

        /// <summary>
        /// Detects strong price reversal patterns
        /// </summary>
        private bool DetectStrongReversal(bool isLong, double atr)
        {
            return isLong ?
                Close[0] < Low[1] - (atr * IntradayReversalATRThreshold) :
                Close[0] > High[1] + (atr * IntradayReversalATRThreshold);
        }

        /// <summary>
        /// Detects momentum changes
        /// </summary>
        private bool DetectMomentumChange(bool isLong)
        {
            return isLong ?
                ROC(MomentumPeriod)[0] < ROC(MomentumPeriod)[1] * MomentumChangeThreshold :
                ROC(MomentumPeriod)[0] > ROC(MomentumPeriod)[1] * MomentumChangeThreshold;
        }

        /// <summary>
        /// Detects moving average alignment changes
        /// </summary>
        private bool DetectMAAlignmentChange(bool isLong)
        {
            return isLong ?
                EMA(TrendEMA1)[0] < EMA(TrendEMA2)[0] :
                EMA(TrendEMA1)[0] > EMA(TrendEMA2)[0];
        }

        /// <summary>
        /// Executes intraday reversal trade
        /// </summary>
        private void ExecuteIntradayReversal(bool isLong, double atr)
        {
            try
            {
                // Calculate potential reversal entry parameters
                double reversalEntryPrice = Close[0];
                double reversalStopPrice = isLong ?
                    reversalEntryPrice + (atr * ATRMultiplier) :
                    reversalEntryPrice - (atr * ATRMultiplier);

                string currentPattern = activePattern;
                
                // Exit current position
                if (isLong)
                {
                    ExitLong(0, Position.Quantity, currentPattern + "-Reversal", currentPattern + "-Entry");
                    Print("Exiting LONG position for intraday reversal");
                }
                else
                {
                    ExitShort(0, Position.Quantity, currentPattern + "-Reversal", currentPattern + "-Entry");
                    Print("Exiting SHORT position for intraday reversal");
                }

                // Enter reversal position with reduced size
                int reversalSize = CalculatePositionSize(reversalEntryPrice, reversalStopPrice);
                reversalSize = Math.Max(1, (int)(reversalSize * IntradayReversalSizeReduction)); // Reduced size for reversals

                string reversalPattern = isLong ? "CBLOHD-Reversal" : "CAHOLD-Reversal";
                
                if (!isLong)
                {
                    EnterLong(reversalSize, reversalPattern);
                    Print($"Entered reversal LONG position at {reversalEntryPrice:F2}, Size: {reversalSize}");
                }
                else
                {
                    EnterShort(reversalSize, reversalPattern);
                    Print($"Entered reversal SHORT position at {reversalEntryPrice:F2}, Size: {reversalSize}");
                }

                // Update tracking variables
                entryPrice = reversalEntryPrice;
                currentStopPrice = reversalStopPrice;
                currentTargetPrice = !isLong ?
                    reversalEntryPrice + (atr * ProfitTargetMultiplier) :
                    reversalEntryPrice - (atr * ProfitTargetMultiplier);

                activePattern = reversalPattern;
            }
            catch (Exception ex)
            {
                Print($"Error in ExecuteIntradayReversal: {ex.Message}");
            }
        }

        /// <summary>
        /// Exits all positions (used for emergency exits and daily limits)
        /// </summary>
        private void ExitAllPositions()
        {
            try
            {
                if (Position.MarketPosition == MarketPosition.Long)
                {
                    ExitLong(0, Position.Quantity, "Exit", activePattern + "-Entry");
                    Print("Exiting all long positions");
                }
                else if (Position.MarketPosition == MarketPosition.Short)
                {
                    ExitShort(0, Position.Quantity, "Exit", activePattern + "-Entry");
                    Print("Exiting all short positions");
                }
            }
            catch (Exception ex)
            {
                Print($"Error in ExitAllPositions: {ex.Message}");
            }
        }

        #endregion
    }
}