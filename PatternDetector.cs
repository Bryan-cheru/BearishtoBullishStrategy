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
    /// Pattern detection functionality for B2B Enhanced strategy
    /// </summary>
    public partial class B2BEnhanced : Strategy
    {
        #region Pattern Detection Methods

        /// <summary>
        /// Updates swing high and low points based on the configured lookback period
        /// </summary>
        private void UpdateSwingPoints()
        {
            try
            {
                if (CurrentBar < SwingPointLookback)
                    return;

                // Reset current bar states
                isSwingHigh[0] = isSwingLow[0] = false;

                // Cache current bar values
                double currentHigh = UseBodyOnly ? Math.Max(Open[0], Close[0]) : High[0];
                double currentLow = UseBodyOnly ? Math.Min(Open[0], Close[0]) : Low[0];

                // Get ATR for volatility-based filtering
                double atr = cachedATR[0];
                double minSwingSize = atr * (MinimumSwingSize / 100.0); // Convert percentage to multiplier

                // Quick pre-check to avoid unnecessary processing
                bool isPotentialSwingHigh = true;
                bool isPotentialSwingLow = true;

                for (int i = 1; i <= SwingPointLookback && (isPotentialSwingHigh || isPotentialSwingLow); i++)
                {
                    if (CurrentBar < i) break;

                    double compareHigh = UseBodyOnly ? Math.Max(Open[i], Close[i]) : High[i];
                    double compareLow = UseBodyOnly ? Math.Min(Open[i], Close[i]) : Low[i];

                    // Early termination if this bar can't possibly be a swing point
                    if (currentHigh <= compareHigh) isPotentialSwingHigh = false;
                    if (currentLow >= compareLow) isPotentialSwingLow = false;
                }

                // Only proceed with full calculation if it passed the pre-check
                double swingHighSize = 0;
                double swingLowSize = 0;

                if (isPotentialSwingHigh)
                {
                    bool isHigherThanRight = true;
                    for (int i = 1; i <= SwingPointLookback && isHigherThanRight; i++)
                    {
                        if (CurrentBar < i) break;
                        double compareHigh = UseBodyOnly ? Math.Max(Open[i], Close[i]) : High[i];
                        double compareLow = UseBodyOnly ? Math.Min(Open[i], Close[i]) : Low[i];
                        isHigherThanRight = currentHigh >= compareHigh;

                        // Calculate swing size
                        swingHighSize = Math.Max(swingHighSize, currentHigh - compareLow);
                    }

                    // Mark as swing high if size requirement met
                    if (isHigherThanRight && swingHighSize >= minSwingSize)
                    {
                        // Volume confirmation if required
                        bool volumeConfirmed = !RequireVolumeConfirmation || Volume[0] > SMA(Volume, VolumeConfirmationPeriod)[0];

                        if (volumeConfirmed)
                        {
                            isSwingHigh[0] = true;
                            swingHigh[0] = currentHigh;
                            swingHighBar[0] = CurrentBar;
                        }
                    }
                }

                if (isPotentialSwingLow)
                {
                    bool isLowerThanRight = true;
                    for (int i = 1; i <= SwingPointLookback && isLowerThanRight; i++)
                    {
                        if (CurrentBar < i) break;
                        double compareHigh = UseBodyOnly ? Math.Max(Open[i], Close[i]) : High[i];
                        double compareLow = UseBodyOnly ? Math.Min(Open[i], Close[i]) : Low[i];
                        isLowerThanRight = currentLow <= compareLow;

                        // Calculate swing size
                        swingLowSize = Math.Max(swingLowSize, compareHigh - currentLow);
                    }

                    // Mark as swing low if size requirement met
                    if (isLowerThanRight && swingLowSize >= minSwingSize)
                    {
                        // Volume confirmation if required
                        bool volumeConfirmed = !RequireVolumeConfirmation || Volume[0] > SMA(Volume, VolumeConfirmationPeriod)[0];

                        if (volumeConfirmed)
                        {
                            isSwingLow[0] = true;
                            swingLow[0] = currentLow;
                            swingLowBar[0] = CurrentBar;
                        }
                    }
                }

                // Don't allow swing high and low on same bar
                if (isSwingHigh[0] && isSwingLow[0])
                {
                    // Prioritize the one with larger swing size
                    if (swingHighSize > swingLowSize)
                    {
                        isSwingLow[0] = false;
                    }
                    else
                    {
                        isSwingHigh[0] = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Print($"Error in UpdateSwingPoints: {ex.Message}");
            }
        }

        /// <summary>
        /// Detects CAHOLD (Close Above High Of Low Day) pattern indicating potential bullish reversal
        /// </summary>
        /// <returns>True if CAHOLD pattern is detected</returns>
        private bool IsCAHOLDPattern()
        {
            try
            {
                if (CurrentBar < Math.Max(BarsRequiredToTrade, PatternLookback))
                    return false;

                // Find the most recent swing low
                double swingLowPrice = double.MaxValue;
                int swingLowIndex = -1;

                for (int i = 1; i <= Math.Min(PatternLookback, CurrentBar); i++)
                {
                    if (isSwingLow[i] && Low[i] < swingLowPrice)
                    {
                        swingLowPrice = Low[i];
                        swingLowIndex = i;
                    }
                }

                // No valid swing low found
                if (swingLowIndex == -1)
                    return false;

                // Get the high of the swing low bar
                double lowDayHigh = UseBodyOnly ?
                    Math.Max(Open[swingLowIndex], Close[swingLowIndex]) :
                    High[swingLowIndex];

                // Core pattern requirements
                bool hasCloseAboveHigh = Close[0] > lowDayHigh;
                bool isBullishCandle = Close[0] > Open[0];
                bool hasMinimumBarsElapsed = (CurrentBar - swingLowIndex) >= MinPatternBarsElapsed;
                bool isInDowntrend = IsInDowntrend();

                // Enhanced quality filters
                bool hasIncreasingVolume = Volume[0] > Volume[1] * VolumeIncreaseThreshold;
                bool hasDecentRange = (High[0] - Low[0]) > cachedATR[0] * MinCandleRangeMultiplier;
                bool hasStrongClose = Close[0] > (High[0] - Low[0]) * StrongCloseThreshold + Low[0];

                // More stringent trend confirmation for higher quality entries
                bool strongDowntrend = EMA(TrendEMA1)[0] < EMA(TrendEMA1)[TrendConfirmationBars1] && 
                                     EMA(TrendEMA2)[0] < EMA(TrendEMA2)[TrendConfirmationBars2];
                bool priceAction = Close[0] < Close[PriceActionLookback];

                // Higher bar quality check
                bool hasRoomToRun = Close[0] < SMA(ResistanceSMA)[0];

                // Volatility filter - avoid trading in extremely low volatility
                bool sufficientVolatility = cachedATR[0] > SMA(cachedATR, VolatilityFilterPeriod)[0] * MinVolatilityThreshold;

                // Combined criteria for quality signals - stricter requirements for higher probability entries
                bool corePattern = hasCloseAboveHigh && isBullishCandle && hasMinimumBarsElapsed && isInDowntrend;
                bool qualityFilters = hasIncreasingVolume && hasDecentRange && hasStrongClose;
                bool additionalConfirmation = hasRoomToRun && sufficientVolatility;

                return corePattern && ((qualityFilters && additionalConfirmation) || !RequireVolumeConfirmation);
            }
            catch (Exception ex)
            {
                Print($"Error in IsCAHOLDPattern: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Detects CBLOHD (Close Below Low Of High Day) pattern indicating potential bearish reversal
        /// </summary>
        /// <returns>True if CBLOHD pattern is detected</returns>
        private bool IsCBLOHDPattern()
        {
            try
            {
                if (CurrentBar < Math.Max(BarsRequiredToTrade, PatternLookback))
                    return false;

                // Find the most recent swing high
                double swingHighPrice = double.MinValue;
                int swingHighIndex = -1;

                for (int i = 1; i <= Math.Min(PatternLookback, CurrentBar); i++)
                {
                    if (isSwingHigh[i] && High[i] > swingHighPrice)
                    {
                        swingHighPrice = High[i];
                        swingHighIndex = i;
                    }
                }

                // No valid swing high found
                if (swingHighIndex == -1)
                    return false;

                // Get the low of the swing high bar
                double highDayLow = UseBodyOnly ?
                    Math.Min(Open[swingHighIndex], Close[swingHighIndex]) :
                    Low[swingHighIndex];

                // Core pattern requirements
                bool hasCloseBelowLow = Close[0] < highDayLow;
                bool isBearishCandle = Close[0] < Open[0];
                bool hasMinimumBarsElapsed = (CurrentBar - swingHighIndex) >= MinPatternBarsElapsed;
                bool isInUptrend = IsInUptrend();

                // Enhanced quality filters
                bool hasIncreasingVolume = Volume[0] > Volume[1] * VolumeIncreaseThreshold;
                bool hasDecentRange = (High[0] - Low[0]) > cachedATR[0] * MinCandleRangeMultiplier;
                bool hasStrongClose = Close[0] < Low[0] + (High[0] - Low[0]) * (1 - StrongCloseThreshold);

                // More stringent trend confirmation for higher quality entries
                bool strongUptrend = EMA(TrendEMA1)[0] > EMA(TrendEMA1)[TrendConfirmationBars1] && 
                                   EMA(TrendEMA2)[0] > EMA(TrendEMA2)[TrendConfirmationBars2];
                bool priceAction = Close[0] > Close[PriceActionLookback];

                // Higher bar quality check
                bool hasRoomToRun = Close[0] > SMA(SupportSMA)[0];

                // Volatility filter - avoid trading in extremely low volatility
                bool sufficientVolatility = cachedATR[0] > SMA(cachedATR, VolatilityFilterPeriod)[0] * MinVolatilityThreshold;

                // Combined criteria for quality signals - stricter requirements for higher probability entries
                bool corePattern = hasCloseBelowLow && isBearishCandle && hasMinimumBarsElapsed && isInUptrend;
                bool qualityFilters = hasIncreasingVolume && hasDecentRange && hasStrongClose;
                bool additionalConfirmation = hasRoomToRun && sufficientVolatility;

                return corePattern && ((qualityFilters && additionalConfirmation) || !RequireVolumeConfirmation);
            }
            catch (Exception ex)
            {
                Print($"Error in IsCBLOHDPattern: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Determines if the market is in a downtrend using multiple confirmation methods
        /// </summary>
        /// <returns>True if downtrend is confirmed</returns>
        private bool IsInDowntrend()
        {
            try
            {
                if (CurrentBar < TrendEMA2) return false;

                // Multiple trend confirmation methods

                // Method 1: Consecutive lower swing highs
                int swingCount = 0;
                double lastSwingHigh = double.MaxValue;

                for (int i = 1; i < SwingTrendLookback; i++)
                {
                    if (isSwingHigh[i])
                    {
                        if (swingHigh[i] < lastSwingHigh)
                        {
                            swingCount++;
                            lastSwingHigh = swingHigh[i];
                        }
                        else break;
                    }
                }

                bool swingHighsConfirm = swingCount >= MinTrendSwings;

                // Method 2: Moving average alignment
                bool emaDowntrend = EMA(TrendEMA1)[0] < EMA(TrendEMA1)[TrendConfirmationBars1] && 
                                  EMA(TrendEMA2)[0] < EMA(TrendEMA2)[TrendConfirmationBars2];

                // Method 3: Price action
                bool priceAction = Close[0] < Close[TrendConfirmationBars1] && Close[TrendConfirmationBars1] < Close[PriceActionLookback];

                // Method 4: Momentum
                bool momentumDown = ROC(MomentumPeriod)[0] < 0;

                // Require at least specified number of methods to confirm
                int confirmationCount = 0;
                if (swingHighsConfirm) confirmationCount++;
                if (emaDowntrend) confirmationCount++;
                if (priceAction) confirmationCount++;
                if (momentumDown) confirmationCount++;

                return confirmationCount >= MinTrendConfirmations;
            }
            catch (Exception ex)
            {
                Print($"Error in IsInDowntrend: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Determines if the market is in an uptrend using multiple confirmation methods
        /// </summary>
        /// <returns>True if uptrend is confirmed</returns>
        private bool IsInUptrend()
        {
            try
            {
                if (CurrentBar < TrendEMA2) return false;

                // Multiple trend confirmation methods

                // Method 1: Consecutive higher swing lows
                int swingCount = 0;
                double lastSwingLow = double.MinValue;

                for (int i = 1; i < SwingTrendLookback; i++)
                {
                    if (isSwingLow[i])
                    {
                        if (swingLow[i] > lastSwingLow)
                        {
                            swingCount++;
                            lastSwingLow = swingLow[i];
                        }
                        else break;
                    }
                }

                bool swingLowsConfirm = swingCount >= MinTrendSwings;

                // Method 2: Moving average alignment
                bool emaUptrend = EMA(TrendEMA1)[0] > EMA(TrendEMA1)[TrendConfirmationBars1] && 
                                EMA(TrendEMA2)[0] > EMA(TrendEMA2)[TrendConfirmationBars2];

                // Method 3: Price action
                bool priceAction = Close[0] > Close[TrendConfirmationBars1] && Close[TrendConfirmationBars1] > Close[PriceActionLookback];

                // Method 4: Momentum
                bool momentumUp = ROC(MomentumPeriod)[0] > 0;

                // Require at least specified number of methods to confirm
                int confirmationCount = 0;
                if (swingLowsConfirm) confirmationCount++;
                if (emaUptrend) confirmationCount++;
                if (priceAction) confirmationCount++;
                if (momentumUp) confirmationCount++;

                return confirmationCount >= MinTrendConfirmations;
            }
            catch (Exception ex)
            {
                Print($"Error in IsInUptrend: {ex.Message}");
                return false;
            }
        }

        #endregion
    }
}