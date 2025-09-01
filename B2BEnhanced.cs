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
    /// Enhanced B2B (Bearish to Bullish) Strategy - Main strategy class
    /// Implements CAHOLD and CBLOHD pattern trading with advanced risk management
    /// </summary>
    public partial class B2BEnhanced : Strategy
    {
        #region Variables
        // Core settings
        private const string STRATEGY_VERSION = "6.0";
        private const string STRATEGY_DATE = "2025-01-01";

        // Trading variables
        private double entryPrice;
        private double currentStopPrice;
        private double currentTargetPrice;
        private string activePattern;
        private double dailyPnL = 0;
        private DateTime currentTradeDay = DateTime.MinValue;
        private int consecutiveLosses = 0;
        private double lastTradeProfit = 0;
        private bool dailyLimitHit = false;
        private bool dailyProfitTargetHit = false;
        private DateTime lastReEvaluationTime = DateTime.MinValue;
        private double initialBalance;
        private double peakBalance;
        private double maxDrawdown = 0;

        // Kelly Criterion variables
        private int totalTrades = 0;
        private int winningTrades = 0;
        private double totalWinAmount = 0;
        private double totalLossAmount = 0;
        private double winRate = 0.5; // Initial estimate
        private double avgWin = 0;
        private double avgLoss = 0;
        private List<double> tradeResults = new List<double>();

        // Pattern detection series
        private Series<double> swingHigh;
        private Series<double> swingLow;
        private Series<int> swingHighBar;
        private Series<int> swingLowBar;
        private Series<bool> isSwingHigh;
        private Series<bool> isSwingLow;

        // Cached indicators for performance
        private Series<double> cachedATR;
        #endregion

        /// <summary>
        /// Called as the strategy transitions through various states
        /// </summary>
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                SetDefaultParameters();
            }
            else if (State == State.Configure)
            {
                ConfigureStrategy();
            }
            else if (State == State.DataLoaded)
            {
                InitializeStrategy();
            }
        }

        /// <summary>
        /// Sets default parameters and strategy information
        /// </summary>
        private void SetDefaultParameters()
        {
            // Strategy information with version at the top
            Description = "Enhanced B2B (Bearish to Bullish) Strategy with modular architecture";
            Name = "B2B Enhanced v" + STRATEGY_VERSION;

            // Core strategy settings
            Calculate = Calculate.OnBarClose;
            EntriesPerDirection = 1;
            EntryHandling = EntryHandling.AllEntries;
            IsExitOnSessionCloseStrategy = true;
            ExitOnSessionCloseSeconds = ExitOnSessionCloseTime;
            IncludeTradeHistoryInBacktest = true;

            // Essential parameters
            RiskPerTrade = 1.0;
            MaxDailyLoss = 3000;
            MaxDailyProfit = 5000;
            UseKellyCriterion = true;
            KellyFraction = 0.5; // Half-Kelly (more conservative)
            MinInitialRisk = 0.5; // Minimum risk percentage
            MaxInitialRisk = 2.0; // Maximum risk percentage
            SwingPointLookback = 2;
            PatternLookback = 20;

            // ATR Settings
            ATRPeriod = 14;
            StopLossATRMultiplier = 2.0;
            ProfitTargetMultiplier = 1.5;

            // Advanced Trade Management
            EnableDynamicStopLoss = true;
            EnableIntradayReversals = true;
            ReEvaluationInterval = 60; // Minutes
            MaxIntradayLoss = 2000;

            // Pattern Settings
            UseBodyOnly = false;
            MinimumSwingSize = 50; // Percentage of ATR
            RequireVolumeConfirmation = true;

            // Position Sizing
            EnablePositionSizeLimit = true;
            MaxPositionSize = 5;
            EnableConsecutiveLossLimit = true;
            MaxConsecutiveLosses = 3;

            // Risk Management
            EnableMaxLoss = true;
            EnableMaxProfit = true;

            // Time filter defaults
            EnableTimeFilter = true;
            UseEasternTime = true;
            Period1Start = new TimeSpan(9, 30, 0);
            Period1End = new TimeSpan(16, 0, 0);
            Period2Start = new TimeSpan(0, 0, 0);
            Period2End = new TimeSpan(0, 0, 0);
            Period3Start = new TimeSpan(0, 0, 0);
            Period3End = new TimeSpan(0, 0, 0);
            Period4Start = new TimeSpan(0, 0, 0);
            Period4End = new TimeSpan(0, 0, 0);

            // Visual Settings
            ShowArrows = true;
            ShowColoredBars = true;
            ShowLabels = false;
            CAHOLDColor = Brushes.DeepSkyBlue;
            CBLOHDColor = Brushes.Gold;
            UpArrowColor = Brushes.LimeGreen;
            DownArrowColor = Brushes.Red;

            // Set configurable magic numbers with default values
            SetMagicNumberDefaults();
        }

        /// <summary>
        /// Sets default values for previously magic numbers
        /// </summary>
        private void SetMagicNumberDefaults()
        {
            // Pattern detection thresholds
            VolumeIncreaseThreshold = 1.2;
            MinCandleRangeMultiplier = 0.5;
            StrongCloseThreshold = 0.7;
            TrendEMA1 = 20;
            TrendEMA2 = 50;
            TrendConfirmationBars1 = 5;
            TrendConfirmationBars2 = 5;
            PriceActionLookback = 10;
            ResistanceSMA = 50;
            SupportSMA = 50;
            VolatilityFilterPeriod = 20;
            MinVolatilityThreshold = 0.7;
            MinPatternBarsElapsed = 2;
            VolumeConfirmationPeriod = 20;

            // Risk management
            MaxRiskPerTradeMultiplier = 0.25;
            StopTighteningFactor = 0.8;
            MaxStopLossDollar = 250;
            MinRewardRiskRatio = 3.0;
            MinTradesForKelly = 10;
            ConsecutiveLossReductionFactor = 0.7;
            MaxConsecutiveLossReduction = 3;
            StopValidationLookback = 5;
            MaxStopDistanceATRMultiplier = 3;
            MaxKellyTradeHistory = 100;

            // Trade management
            FirstTargetSizeRatio = 0.33;
            SecondTargetSizeRatio = 0.33;
            FirstTargetRatio = 1.5;
            SecondTargetRatio = 3.0;
            FinalTargetRatio = 5.0;
            BreakevenProfitATR = 0.5;
            TrailingStartATR = 1.0;
            Stage1TrailFactor = 0.5;
            Stage2TrailFactor = 0.4;
            Stage3TrailFactor = 0.3;
            FinalStageTrailFactor = 0.25;
            Stage2TrailATR = 2.0;
            Stage3TrailATR = 3.0;
            FinalStageTrailATR = 4.0;
            VolatilityComparisonBars = 5;
            MinProfitPotential = 0.5;
            IntradayReversalVolumeThreshold = 1.2;
            IntradayReversalATRThreshold = 1.5;
            MomentumChangeThreshold = 0.5;
            IntradayReversalSizeReduction = 0.5;

            // Trend detection
            SwingTrendLookback = 10;
            MinTrendSwings = 2;
            MomentumPeriod = 14;
            MinTrendConfirmations = 2;

            // Session management
            ExitOnSessionCloseTime = 3600;
        }

        /// <summary>
        /// Configures strategy-specific settings
        /// </summary>
        private void ConfigureStrategy()
        {
            // Add plots for patterns
            AddPlot(new Stroke(CAHOLDColor, 2), PlotStyle.Block, "CAHOLD");
            AddPlot(new Stroke(CBLOHDColor, 2), PlotStyle.Block, "CBLOHD");

            // Set PatternLookback based on timeframe for optimal performance
            if (BarsPeriod.BarsPeriodType == BarsPeriodType.Minute)
            {
                switch (BarsPeriod.Value)
                {
                    case 1:
                        PatternLookback = 25; // 1-minute chart
                        break;
                    case 5:
                        PatternLookback = 20; // 5-minute chart
                        break;
                    case 15:
                        PatternLookback = 15; // 15-minute chart
                        break;
                    case 30:
                        PatternLookback = 12; // 30-minute chart
                        break;
                    case 60:
                        PatternLookback = 10; // 1-hour chart
                        break;
                    default:
                        PatternLookback = 20; // Default value
                        break;
                }
            }
            else if (BarsPeriod.BarsPeriodType == BarsPeriodType.Day)
            {
                PatternLookback = 8; // Daily chart
            }

            // Set BarsRequiredToTrade
            BarsRequiredToTrade = Math.Max(20, PatternLookback + 5);
        }

        /// <summary>
        /// Initializes strategy after data is loaded
        /// </summary>
        private void InitializeStrategy()
        {
            // Initialize series
            swingHigh = new Series<double>(this);
            swingLow = new Series<double>(this);
            swingHighBar = new Series<int>(this);
            swingLowBar = new Series<int>(this);
            isSwingHigh = new Series<bool>(this);
            isSwingLow = new Series<bool>(this);

            // Initialize cached indicators for performance
            cachedATR = new Series<double>(this);

            // Initialize account tracking
            initialBalance = Account.Get(AccountItem.CashValue, Currency.UsDollar);
            peakBalance = initialBalance;

            Print($"B2B Enhanced v{STRATEGY_VERSION} initialized. Initial balance: ${initialBalance}");
        }

        /// <summary>
        /// Called on each bar update - main strategy logic
        /// </summary>
        protected override void OnBarUpdate()
        {
            try
            {
                // Skip if not enough bars
                if (CurrentBar < BarsRequiredToTrade)
                {
                    if (IsFirstTickOfBar)
                        Print($"Waiting for sufficient bars: {CurrentBar}/{BarsRequiredToTrade}");
                    return;
                }

                // Skip if not first tick of bar for cleaner processing
                if (!IsFirstTickOfBar && Calculate == Calculate.OnBarClose)
                    return;

                // Update cached indicators
                cachedATR[0] = ATR(ATRPeriod)[0];

                // Update daily metrics
                if (IsNewTradingDay())
                {
                    ResetDailyMetrics();
                }

                // Update swing points first since pattern detection depends on them
                UpdateSwingPoints();

                // Check for daily loss or profit limits
                if (IsDailyLossLimitReached() || IsDailyProfitTargetReached())
                {
                    return;
                }

                // Skip if outside trading hours
                if (EnableTimeFilter && !IsWithinTradingHours())
                {
                    return;
                }

                // Detect patterns
                bool isCAHOLD = IsCAHOLDPattern();
                bool isCBLOHD = IsCBLOHDPattern();

                // Visualize patterns if enabled
                if (ShowColoredBars || ShowArrows)
                {
                    ApplyPatternVisualization(isCAHOLD, isCBLOHD);
                }

                // Position management
                if (Position.MarketPosition != MarketPosition.Flat)
                {
                    ManageActivePosition();

                    // Re-evaluate position periodically
                    if (EnableDynamicStopLoss &&
                        Time[0] >= lastReEvaluationTime.AddMinutes(ReEvaluationInterval))
                    {
                        ReEvaluatePosition();
                        lastReEvaluationTime = Time[0];
                    }

                    // Check for intraday reversals
                    if (EnableIntradayReversals)
                    {
                        CheckForIntradayReversal();
                    }
                }
                // Check for new entry signals
                else if (!dailyLimitHit && !dailyProfitTargetHit)
                {
                    double atr = cachedATR[0];

                    // Process CAHOLD entry
                    if (isCAHOLD)
                    {
                        ProcessCAHOLDEntry(atr);
                    }
                    // Process CBLOHD entry
                    else if (isCBLOHD)
                    {
                        ProcessCBLOHDEntry(atr);
                    }
                }
            }
            catch (Exception ex)
            {
                Print("ERROR in OnBarUpdate: " + ex.Message);
                Print("Stack Trace: " + ex.StackTrace);

                // Safety measure - close positions on error
                if (Position.MarketPosition != MarketPosition.Flat)
                {
                    ExitAllPositions();
                }
            }
        }

        /// <summary>
        /// Called when an execution occurs
        /// </summary>
        /// <param name="execution">Execution details</param>
        /// <param name="executionId">Execution ID</param>
        /// <param name="price">Execution price</param>
        /// <param name="quantity">Execution quantity</param>
        /// <param name="marketPosition">Market position after execution</param>
        /// <param name="orderId">Order ID</param>
        /// <param name="time">Execution time</param>
        protected override void OnExecutionUpdate(Execution execution, string executionId, double price, int quantity, MarketPosition marketPosition, string orderId, DateTime time)
        {
            try
            {
                if (execution.Order.OrderState == OrderState.Filled || execution.Order.OrderState == OrderState.PartFilled)
                {
                    // Check if this is an entry
                    if (execution.Order.Name.EndsWith("-Entry") || execution.Order.Name.EndsWith("-Reversal"))
                    {
                        ProcessEntryExecution(execution);
                    }
                    // Check if this is an exit
                    else if (IsExitOrder(execution.Order.Name))
                    {
                        ProcessExitExecution(execution);
                    }
                }
            }
            catch (Exception ex)
            {
                Print($"Error in OnExecutionUpdate: {ex.Message}");
            }
        }

        /// <summary>
        /// Processes entry order executions
        /// </summary>
        /// <param name="execution">Execution details</param>
        private void ProcessEntryExecution(Execution execution)
        {
            entryPrice = execution.Price;

            // Set initial stop and target prices
            if (execution.Order.Name.Contains("CAHOLD"))
            {
                currentStopPrice = entryPrice - (cachedATR[0] * StopLossATRMultiplier);
                currentTargetPrice = entryPrice + (cachedATR[0] * ProfitTargetMultiplier);
                activePattern = "CAHOLD";
            }
            else if (execution.Order.Name.Contains("CBLOHD"))
            {
                currentStopPrice = entryPrice + (cachedATR[0] * StopLossATRMultiplier);
                currentTargetPrice = entryPrice - (cachedATR[0] * ProfitTargetMultiplier);
                activePattern = "CBLOHD";
            }

            Print($"Position entered at {entryPrice:F2}. Stop: {currentStopPrice:F2}, Target: {currentTargetPrice:F2}");
        }

        /// <summary>
        /// Checks if an order is an exit order
        /// </summary>
        /// <param name="orderName">Order name to check</param>
        /// <returns>True if it's an exit order</returns>
        private bool IsExitOrder(string orderName)
        {
            return orderName.Contains("SL") || orderName.Contains("PT") ||
                   orderName.Contains("Trail") || orderName.Contains("Exit") ||
                   orderName.Contains("Vol") || orderName.Contains("Reeval") ||
                   orderName.Contains("Reversal");
        }

        /// <summary>
        /// Processes exit order executions
        /// </summary>
        /// <param name="execution">Execution details</param>
        private void ProcessExitExecution(Execution execution)
        {
            // Calculate profit/loss
            double tradePnL = CalculateTradePnL(execution);

            // Update trade statistics
            lastTradeProfit = tradePnL;
            dailyPnL += tradePnL;

            // Update Kelly Criterion statistics
            UpdateKellyStatistics(tradePnL);

            // Log exit information
            Print($"Position exited at {execution.Price:F2}. P&L: ${tradePnL:F2}, Daily P&L: ${dailyPnL:F2}");

            // Track cumulative performance
            TrackPerformance();

            // Reset position tracking if completely flat
            if (Position.MarketPosition == MarketPosition.Flat)
            {
                activePattern = "";
            }
        }

        /// <summary>
        /// Calculates profit/loss for a trade execution
        /// </summary>
        /// <param name="execution">Execution details</param>
        /// <returns>Profit/Loss amount</returns>
        private double CalculateTradePnL(Execution execution)
        {
            double tradePnL = 0;

            if (execution.Order.OrderAction == OrderAction.Sell)
            {
                // For selling a long position
                tradePnL = (execution.Price - entryPrice) * execution.Quantity * Instrument.MasterInstrument.PointValue;
            }
            else if (execution.Order.OrderAction == OrderAction.BuyToCover)
            {
                // For covering a short position
                tradePnL = (entryPrice - execution.Price) * execution.Quantity * Instrument.MasterInstrument.PointValue;
            }

            return tradePnL;
        }

        /// <summary>
        /// Tracks overall strategy performance
        /// </summary>
        private void TrackPerformance()
        {
            double accountValue = Account.Get(AccountItem.CashValue, Currency.UsDollar);
            double totalReturn = (accountValue - initialBalance) / initialBalance;

            UpdateDrawdownTracking();

            Print($"Total Return: {totalReturn:P2}, Max Drawdown: {maxDrawdown:P2}");
        }

        #region Helper Methods

        /// <summary>
        /// Applies pattern visualization on the chart
        /// </summary>
        /// <param name="isCAHOLD">True if CAHOLD pattern detected</param>
        /// <param name="isCBLOHD">True if CBLOHD pattern detected</param>
        private void ApplyPatternVisualization(bool isCAHOLD, bool isCBLOHD)
        {
            try
            {
                // Update plot values for visualization
                if (ShowColoredBars)
                {
                    Values[0][0] = isCAHOLD ? High[0] : double.NaN;  // CAHOLD plot
                    Values[1][0] = isCBLOHD ? Low[0] : double.NaN;   // CBLOHD plot
                }

                // Draw arrows if enabled
                if (ShowArrows)
                {
                    if (isCAHOLD)
                    {
                        // Draw up arrow below the CAHOLD candle
                        double arrowY = Low[0] - (cachedATR[0] * 0.2);
                        Draw.ArrowUp(this, "UpArrow_" + CurrentBar, false, 0, arrowY, UpArrowColor);

                        // Optional label
                        if (ShowLabels)
                        {
                            double labelY = Low[0] - (cachedATR[0] * 0.4);
                            Draw.Text(this, "CAHOLDLabel_" + CurrentBar, "CAHOLD", 0, labelY, UpArrowColor);
                        }
                    }

                    if (isCBLOHD)
                    {
                        // Draw down arrow above the CBLOHD candle
                        double arrowY = High[0] + (cachedATR[0] * 0.2);
                        Draw.ArrowDown(this, "DownArrow_" + CurrentBar, false, 0, arrowY, DownArrowColor);

                        // Optional label
                        if (ShowLabels)
                        {
                            double labelY = High[0] + (cachedATR[0] * 0.4);
                            Draw.Text(this, "CBLOHDLabel_" + CurrentBar, "CBLOHD", 0, labelY, DownArrowColor);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Print($"Error in ApplyPatternVisualization: {ex.Message}");
            }
        }

        /// <summary>
        /// Converts time to specified timezone
        /// </summary>
        /// <param name="time">Time to convert</param>
        /// <returns>Converted time</returns>
        private DateTime GetAdjustedTime(DateTime time)
        {
            if (!UseEasternTime)
                return time;

            try
            {
                // Convert to Eastern Time
                TimeZoneInfo easternZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                return TimeZoneInfo.ConvertTimeFromUtc(time.ToUniversalTime(), easternZone);
            }
            catch (TimeZoneNotFoundException)
            {
                Print("Warning: Eastern Time Zone not found. Using local time instead.");
                return time;
            }
            catch (Exception ex)
            {
                Print($"Time conversion error: {ex.Message}. Using local time instead.");
                return time;
            }
        }

        /// <summary>
        /// Checks if current time is within trading hours
        /// </summary>
        /// <returns>True if within trading hours</returns>
        private bool IsWithinTradingHours()
        {
            if (!EnableTimeFilter)
                return true;

            DateTime adjustedTime = GetAdjustedTime(Time[0]);
            TimeSpan timeOfDay = adjustedTime.TimeOfDay;

            bool withinPeriod1 = IsWithinPeriod(timeOfDay, Period1Start, Period1End);
            bool withinPeriod2 = IsWithinPeriod(timeOfDay, Period2Start, Period2End);
            bool withinPeriod3 = IsWithinPeriod(timeOfDay, Period3Start, Period3End);
            bool withinPeriod4 = IsWithinPeriod(timeOfDay, Period4Start, Period4End);

            return withinPeriod1 || withinPeriod2 || withinPeriod3 || withinPeriod4;
        }

        /// <summary>
        /// Checks if time is within specified period
        /// </summary>
        /// <param name="currentTime">Current time</param>
        /// <param name="start">Period start time</param>
        /// <param name="end">Period end time</param>
        /// <returns>True if within period</returns>
        private bool IsWithinPeriod(TimeSpan currentTime, TimeSpan start, TimeSpan end)
        {
            if (start == end) // Period not set
                return false;

            if (start < end)
                return currentTime >= start && currentTime <= end;
            else // Overnight period
                return currentTime >= start || currentTime <= end;
        }

        /// <summary>
        /// Checks if it's a new trading day
        /// </summary>
        /// <returns>True if new trading day</returns>
        private bool IsNewTradingDay()
        {
            DateTime currentDate = GetAdjustedTime(Time[0]).Date;

            if (currentTradeDay.Date != currentDate)
            {
                currentTradeDay = currentDate;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Resets daily metrics for new trading day
        /// </summary>
        private void ResetDailyMetrics()
        {
            dailyLimitHit = false;
            dailyProfitTargetHit = false;
            dailyPnL = 0;

            // Update account balance tracking
            double currentBalance = Account.Get(AccountItem.CashValue, Currency.UsDollar);

            UpdateDrawdownTracking();

            Print($"New trading day: {currentTradeDay.ToShortDateString()}");
            Print($"Account balance: ${currentBalance:F2}, Peak: ${peakBalance:F2}, Max Drawdown: {maxDrawdown:P2}");
        }

        #endregion

        #region Properties
        // Version information (required at top)
        /// <summary>
        /// Strategy version number
        /// </summary>
        [XmlIgnore]
        [Display(Name = "Version",
                 Description = "Strategy version",
                 Order = 1,
                 GroupName = "1. Version Info")]
        public string Version
        {
            get { return STRATEGY_VERSION; }
            set { /* read-only */ }
        }

        /// <summary>
        /// Strategy version date
        /// </summary>
        [XmlIgnore]
        [Display(Name = "Version Date",
                 Description = "Last update date",
                 Order = 2,
                 GroupName = "1. Version Info")]
        public string VersionDate
        {
            get { return STRATEGY_DATE; }
            set { /* read-only */ }
        }

        // Account/Risk settings (high priority)
        /// <summary>
        /// Base percentage of account to risk per trade
        /// </summary>
        [NinjaScriptProperty]
        [Range(0.1, 5.0)]
        [Display(Name = "Risk Per Trade (%)",
                 Description = "Base percentage of account to risk per trade",
                 Order = 1,
                 GroupName = "2. Account/Risk")]
        public double RiskPerTrade { get; set; }

        /// <summary>
        /// Enable Kelly Criterion for position sizing
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Use Kelly Criterion",
                 Description = "Use Kelly Criterion for position sizing",
                 Order = 2,
                 GroupName = "2. Account/Risk")]
        public bool UseKellyCriterion { get; set; }

        /// <summary>
        /// Fraction of Kelly to use (lower is more conservative)
        /// </summary>
        [NinjaScriptProperty]
        [Range(0.1, 1.0)]
        [Display(Name = "Kelly Fraction",
                 Description = "Fraction of Kelly to use (lower is more conservative)",
                 Order = 3,
                 GroupName = "2. Account/Risk")]
        public double KellyFraction { get; set; }

        /// <summary>
        /// Minimum risk percentage per trade
        /// </summary>
        [NinjaScriptProperty]
        [Range(0.1, 5.0)]
        [Display(Name = "Min Initial Risk (%)",
                 Description = "Minimum risk percentage per trade",
                 Order = 4,
                 GroupName = "2. Account/Risk")]
        public double MinInitialRisk { get; set; }

        /// <summary>
        /// Maximum risk percentage per trade
        /// </summary>
        [NinjaScriptProperty]
        [Range(0.1, 5.0)]
        [Display(Name = "Max Initial Risk (%)",
                 Description = "Maximum risk percentage per trade",
                 Order = 5,
                 GroupName = "2. Account/Risk")]
        public double MaxInitialRisk { get; set; }

        /// <summary>
        /// Enable daily max loss limit
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Enable Max Loss",
                 Description = "Enable daily max loss limit",
                 Order = 6,
                 GroupName = "2. Account/Risk")]
        public bool EnableMaxLoss { get; set; }

        /// <summary>
        /// Maximum daily loss in dollars
        /// </summary>
        [NinjaScriptProperty]
        [Range(100, 10000)]
        [Display(Name = "Max Daily Loss ($)",
                 Description = "Maximum daily loss in dollars",
                 Order = 7,
                 GroupName = "2. Account/Risk")]
        public double MaxDailyLoss { get; set; }

        /// <summary>
        /// Enable daily max profit target
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Enable Max Profit",
                 Description = "Enable daily max profit target",
                 Order = 8,
                 GroupName = "2. Account/Risk")]
        public bool EnableMaxProfit { get; set; }

        /// <summary>
        /// Maximum daily profit target in dollars
        /// </summary>
        [NinjaScriptProperty]
        [Range(100, 20000)]
        [Display(Name = "Max Daily Profit ($)",
                 Description = "Maximum daily profit target in dollars",
                 Order = 9,
                 GroupName = "2. Account/Risk")]
        public double MaxDailyProfit { get; set; }

        /// <summary>
        /// Maximum loss amount before stopping intraday trading
        /// </summary>
        [NinjaScriptProperty]
        [Range(100, 10000)]
        [Display(Name = "Max Intraday Loss ($)",
                 Description = "Maximum loss amount before stopping intraday trading",
                 Order = 10,
                 GroupName = "2. Account/Risk")]
        public double MaxIntradayLoss { get; set; }

        // Position Management
        /// <summary>
        /// Enable maximum position size limit
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Enable Position Size Limit",
                 Description = "Enable maximum position size limit",
                 Order = 1,
                 GroupName = "3. Position Management")]
        public bool EnablePositionSizeLimit { get; set; }

        /// <summary>
        /// Maximum allowed position size
        /// </summary>
        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "Max Position Size",
                 Description = "Maximum allowed position size",
                 Order = 2,
                 GroupName = "3. Position Management")]
        public int MaxPositionSize { get; set; }

        /// <summary>
        /// Enable consecutive loss management
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Enable Consecutive Loss Limit",
                 Description = "Enable consecutive loss management",
                 Order = 3,
                 GroupName = "3. Position Management")]
        public bool EnableConsecutiveLossLimit { get; set; }

        /// <summary>
        /// Maximum consecutive losses before reducing risk
        /// </summary>
        [NinjaScriptProperty]
        [Range(1, 10)]
        [Display(Name = "Max Consecutive Losses",
                 Description = "Maximum consecutive losses before reducing risk",
                 Order = 4,
                 GroupName = "3. Position Management")]
        public int MaxConsecutiveLosses { get; set; }

        /// <summary>
        /// Enable dynamic stop-loss management
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Enable Dynamic Stop Loss",
                 Description = "Enable dynamic stop-loss management",
                 Order = 5,
                 GroupName = "3. Position Management")]
        public bool EnableDynamicStopLoss { get; set; }

        /// <summary>
        /// Enable intraday trend reversal detection
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Enable Intraday Reversals",
                 Description = "Enable intraday trend reversal detection",
                 Order = 6,
                 GroupName = "3. Position Management")]
        public bool EnableIntradayReversals { get; set; }

        /// <summary>
        /// Minutes between position re-evaluations
        /// </summary>
        [NinjaScriptProperty]
        [Range(5, 240)]
        [Display(Name = "Re-Evaluation Interval",
                 Description = "Minutes between position re-evaluations",
                 Order = 7,
                 GroupName = "3. Position Management")]
        public int ReEvaluationInterval { get; set; }

        // ATR Settings
        /// <summary>
        /// Period for ATR calculation
        /// </summary>
        [NinjaScriptProperty]
        [Range(1, 50)]
        [Display(Name = "ATR Period",
                 Description = "Period for ATR calculation",
                 Order = 1,
                 GroupName = "4. ATR Settings")]
        public int ATRPeriod { get; set; }

        /// <summary>
        /// Multiplier for ATR-based stops
        /// </summary>
        [NinjaScriptProperty]
        [Range(0.1, 10.0)]
        [Display(Name = "Stop Loss ATR Multiplier",
                 Description = "Multiplier for ATR-based stops",
                 Order = 2,
                 GroupName = "4. ATR Settings")]
        public double StopLossATRMultiplier { get; set; }

        /// <summary>
        /// Multiplier for ATR-based profit targets
        /// </summary>
        [NinjaScriptProperty]
        [Range(0.1, 10.0)]
        [Display(Name = "Profit Target Multiplier",
                 Description = "Multiplier for ATR-based profit targets",
                 Order = 3,
                 GroupName = "4. ATR Settings")]
        public double ProfitTargetMultiplier { get; set; }

        // Pattern Detection
        /// <summary>
        /// Use only candle body for calculations
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Use Body Only",
                 Description = "Use only candle body for calculations",
                 Order = 1,
                 GroupName = "5. Pattern Detection")]
        public bool UseBodyOnly { get; set; }

        /// <summary>
        /// Number of bars to confirm swing points
        /// </summary>
        [NinjaScriptProperty]
        [Range(2, 10)]
        [Display(Name = "Swing Point Lookback",
                 Description = "Number of bars to confirm swing points",
                 Order = 2,
                 GroupName = "5. Pattern Detection")]
        public int SwingPointLookback { get; set; }

        /// <summary>
        /// Number of bars to look back for pattern detection
        /// </summary>
        [NinjaScriptProperty]
        [Range(0, 50)]
        [Display(Name = "Pattern Lookback",
                 Description = "Number of bars to look back for pattern detection",
                 Order = 3,
                 GroupName = "5. Pattern Detection")]
        public int PatternLookback { get; set; }

        /// <summary>
        /// Minimum swing size as percentage of ATR
        /// </summary>
        [NinjaScriptProperty]
        [Range(10, 100)]
        [Display(Name = "Minimum Swing Size (%ATR)",
                 Description = "Minimum swing size as percentage of ATR",
                 Order = 4,
                 GroupName = "5. Pattern Detection")]
        public double MinimumSwingSize { get; set; }

        /// <summary>
        /// Require above-average volume for pattern confirmation
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Require Volume Confirmation",
                 Description = "Require above-average volume for pattern confirmation",
                 Order = 5,
                 GroupName = "5. Pattern Detection")]
        public bool RequireVolumeConfirmation { get; set; }

        // Time Filter
        /// <summary>
        /// Enable time-based trading filter
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Enable Time Filter",
                 Description = "Enable time-based trading filter",
                 Order = 1,
                 GroupName = "6. Time Filter")]
        public bool EnableTimeFilter { get; set; }

        /// <summary>
        /// Use Eastern Time (ET) for time filtering
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Use Eastern Time",
                 Description = "Use Eastern Time (ET) for time filtering",
                 Order = 2,
                 GroupName = "6. Time Filter")]
        public bool UseEasternTime { get; set; }

        /// <summary>
        /// Start time for trading period 1
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Period 1 Start",
                 Description = "Start time for trading period 1",
                 Order = 3,
                 GroupName = "6. Time Filter")]
        public TimeSpan Period1Start { get; set; }

        /// <summary>
        /// End time for trading period 1
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Period 1 End",
                 Description = "End time for trading period 1",
                 Order = 4,
                 GroupName = "6. Time Filter")]
        public TimeSpan Period1End { get; set; }

        /// <summary>
        /// Start time for trading period 2
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Period 2 Start",
                 Description = "Start time for trading period 2",
                 Order = 5,
                 GroupName = "6. Time Filter")]
        public TimeSpan Period2Start { get; set; }

        /// <summary>
        /// End time for trading period 2
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Period 2 End",
                 Description = "End time for trading period 2",
                 Order = 6,
                 GroupName = "6. Time Filter")]
        public TimeSpan Period2End { get; set; }

        /// <summary>
        /// Start time for trading period 3
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Period 3 Start",
                 Description = "Start time for trading period 3",
                 Order = 7,
                 GroupName = "6. Time Filter")]
        public TimeSpan Period3Start { get; set; }

        /// <summary>
        /// End time for trading period 3
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Period 3 End",
                 Description = "End time for trading period 3",
                 Order = 8,
                 GroupName = "6. Time Filter")]
        public TimeSpan Period3End { get; set; }

        /// <summary>
        /// Start time for trading period 4
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Period 4 Start",
                 Description = "Start time for trading period 4",
                 Order = 9,
                 GroupName = "6. Time Filter")]
        public TimeSpan Period4Start { get; set; }

        /// <summary>
        /// End time for trading period 4
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Period 4 End",
                 Description = "End time for trading period 4",
                 Order = 10,
                 GroupName = "6. Time Filter")]
        public TimeSpan Period4End { get; set; }

        // Visual Settings
        /// <summary>
        /// Show pattern arrows
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Show Arrows",
                 Description = "Show pattern arrows",
                 Order = 1,
                 GroupName = "7. Visualization")]
        public bool ShowArrows { get; set; }

        /// <summary>
        /// Show colored bars for patterns
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Show Colored Bars",
                 Description = "Show colored bars for patterns",
                 Order = 2,
                 GroupName = "7. Visualization")]
        public bool ShowColoredBars { get; set; }

        /// <summary>
        /// Show pattern labels
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Show Labels",
                 Description = "Show pattern labels",
                 Order = 3,
                 GroupName = "7. Visualization")]
        public bool ShowLabels { get; set; }

        /// <summary>
        /// Color for CAHOLD pattern
        /// </summary>
        [XmlIgnore]
        [Display(Name = "CAHOLD Color",
                 Description = "Color for CAHOLD pattern",
                 Order = 4,
                 GroupName = "7. Visualization")]
        public Brush CAHOLDColor { get; set; }

        /// <summary>
        /// Color for CBLOHD pattern
        /// </summary>
        [XmlIgnore]
        [Display(Name = "CBLOHD Color",
                 Description = "Color for CBLOHD pattern",
                 Order = 5,
                 GroupName = "7. Visualization")]
        public Brush CBLOHDColor { get; set; }

        /// <summary>
        /// Color for bullish arrows
        /// </summary>
        [XmlIgnore]
        [Display(Name = "Up Arrow Color",
                 Description = "Color for bullish arrows",
                 Order = 6,
                 GroupName = "7. Visualization")]
        public Brush UpArrowColor { get; set; }

        /// <summary>
        /// Color for bearish arrows
        /// </summary>
        [XmlIgnore]
        [Display(Name = "Down Arrow Color",
                 Description = "Color for bearish arrows",
                 Order = 7,
                 GroupName = "7. Visualization")]
        public Brush DownArrowColor { get; set; }

        // Additional NinjaTrader settings
        /// <summary>
        /// Include detailed trade history in backtest results
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Include Trade History In Backtest",
                 Description = "Include detailed trade history in backtest results",
                 Order = 1,
                 GroupName = "8. Backtest Settings")]
        public bool IncludeTradeHistoryInBacktest { get; set; }

        // Configurable Magic Numbers - now exposed as properties with Range attributes

        #region Pattern Detection Configuration
        /// <summary>
        /// Volume increase threshold for pattern confirmation
        /// </summary>
        [NinjaScriptProperty]
        [Range(1.0, 3.0)]
        [Display(Name = "Volume Increase Threshold",
                 Description = "Multiplier for volume increase requirement (e.g. 1.2 = 20% increase)",
                 Order = 1,
                 GroupName = "9. Pattern Thresholds")]
        public double VolumeIncreaseThreshold { get; set; }

        /// <summary>
        /// Minimum candle range as ATR multiplier
        /// </summary>
        [NinjaScriptProperty]
        [Range(0.1, 2.0)]
        [Display(Name = "Min Candle Range ATR Multiplier",
                 Description = "Minimum candle range as ATR multiplier for pattern validity",
                 Order = 2,
                 GroupName = "9. Pattern Thresholds")]
        public double MinCandleRangeMultiplier { get; set; }

        /// <summary>
        /// Strong close threshold (percentage of candle range)
        /// </summary>
        [NinjaScriptProperty]
        [Range(0.5, 0.9)]
        [Display(Name = "Strong Close Threshold",
                 Description = "Close position within candle range for strong close (0.7 = upper 30%)",
                 Order = 3,
                 GroupName = "9. Pattern Thresholds")]
        public double StrongCloseThreshold { get; set; }

        /// <summary>
        /// Fast EMA period for trend detection
        /// </summary>
        [NinjaScriptProperty]
        [Range(5, 50)]
        [Display(Name = "Trend EMA 1 Period",
                 Description = "Fast EMA period for trend confirmation",
                 Order = 4,
                 GroupName = "9. Pattern Thresholds")]
        public int TrendEMA1 { get; set; }

        /// <summary>
        /// Slow EMA period for trend detection
        /// </summary>
        [NinjaScriptProperty]
        [Range(20, 100)]
        [Display(Name = "Trend EMA 2 Period",
                 Description = "Slow EMA period for trend confirmation",
                 Order = 5,
                 GroupName = "9. Pattern Thresholds")]
        public int TrendEMA2 { get; set; }
        #endregion

        #region Risk Management Configuration
        /// <summary>
        /// Stop tightening factor to reduce average loss
        /// </summary>
        [NinjaScriptProperty]
        [Range(0.5, 1.0)]
        [Display(Name = "Stop Tightening Factor",
                 Description = "Factor to tighten stops (0.8 = 20% tighter than ATR multiplier)",
                 Order = 1,
                 GroupName = "10. Risk Configuration")]
        public double StopTighteningFactor { get; set; }

        /// <summary>
        /// Maximum stop loss in dollars
        /// </summary>
        [NinjaScriptProperty]
        [Range(100, 1000)]
        [Display(Name = "Max Stop Loss ($)",
                 Description = "Maximum stop loss amount in dollars",
                 Order = 2,
                 GroupName = "10. Risk Configuration")]
        public double MaxStopLossDollar { get; set; }

        /// <summary>
        /// Minimum reward/risk ratio for trades
        /// </summary>
        [NinjaScriptProperty]
        [Range(1.0, 10.0)]
        [Display(Name = "Min Reward/Risk Ratio",
                 Description = "Minimum reward to risk ratio for trade entries",
                 Order = 3,
                 GroupName = "10. Risk Configuration")]
        public double MinRewardRiskRatio { get; set; }

        /// <summary>
        /// Consecutive loss reduction factor
        /// </summary>
        [NinjaScriptProperty]
        [Range(0.5, 0.9)]
        [Display(Name = "Consecutive Loss Reduction Factor",
                 Description = "Factor to reduce position size after consecutive losses",
                 Order = 4,
                 GroupName = "10. Risk Configuration")]
        public double ConsecutiveLossReductionFactor { get; set; }
        #endregion

        #region Trade Management Configuration
        /// <summary>
        /// First profit target ratio
        /// </summary>
        [NinjaScriptProperty]
        [Range(1.0, 5.0)]
        [Display(Name = "First Target Ratio",
                 Description = "Risk/reward ratio for first profit target",
                 Order = 1,
                 GroupName = "11. Target Configuration")]
        public double FirstTargetRatio { get; set; }

        /// <summary>
        /// Second profit target ratio
        /// </summary>
        [NinjaScriptProperty]
        [Range(2.0, 8.0)]
        [Display(Name = "Second Target Ratio",
                 Description = "Risk/reward ratio for second profit target",
                 Order = 2,
                 GroupName = "11. Target Configuration")]
        public double SecondTargetRatio { get; set; }

        /// <summary>
        /// Final profit target ratio
        /// </summary>
        [NinjaScriptProperty]
        [Range(3.0, 15.0)]
        [Display(Name = "Final Target Ratio",
                 Description = "Risk/reward ratio for final profit target",
                 Order = 3,
                 GroupName = "11. Target Configuration")]
        public double FinalTargetRatio { get; set; }

        /// <summary>
        /// Breakeven profit threshold in ATR units
        /// </summary>
        [NinjaScriptProperty]
        [Range(0.1, 2.0)]
        [Display(Name = "Breakeven Profit ATR",
                 Description = "Profit in ATR units before moving stop to breakeven",
                 Order = 4,
                 GroupName = "11. Target Configuration")]
        public double BreakevenProfitATR { get; set; }

        /// <summary>
        /// Trailing start threshold in ATR units
        /// </summary>
        [NinjaScriptProperty]
        [Range(0.5, 5.0)]
        [Display(Name = "Trailing Start ATR",
                 Description = "Profit in ATR units before starting trailing stop",
                 Order = 5,
                 GroupName = "11. Target Configuration")]
        public double TrailingStartATR { get; set; }
        #endregion

        // Internal configuration properties (not exposed to UI but configurable)
        private int TrendConfirmationBars1 { get; set; }
        private int TrendConfirmationBars2 { get; set; }
        private int PriceActionLookback { get; set; }
        private int ResistanceSMA { get; set; }
        private int SupportSMA { get; set; }
        private int VolatilityFilterPeriod { get; set; }
        private double MinVolatilityThreshold { get; set; }
        private int MinPatternBarsElapsed { get; set; }
        private int VolumeConfirmationPeriod { get; set; }
        private double MaxRiskPerTradeMultiplier { get; set; }
        private int MinTradesForKelly { get; set; }
        private int MaxConsecutiveLossReduction { get; set; }
        private int StopValidationLookback { get; set; }
        private double MaxStopDistanceATRMultiplier { get; set; }
        private int MaxKellyTradeHistory { get; set; }
        private double FirstTargetSizeRatio { get; set; }
        private double SecondTargetSizeRatio { get; set; }
        private double Stage1TrailFactor { get; set; }
        private double Stage2TrailFactor { get; set; }
        private double Stage3TrailFactor { get; set; }
        private double FinalStageTrailFactor { get; set; }
        private double Stage2TrailATR { get; set; }
        private double Stage3TrailATR { get; set; }
        private double FinalStageTrailATR { get; set; }
        private int VolatilityComparisonBars { get; set; }
        private double MinProfitPotential { get; set; }
        private double IntradayReversalVolumeThreshold { get; set; }
        private double IntradayReversalATRThreshold { get; set; }
        private double MomentumChangeThreshold { get; set; }
        private double IntradayReversalSizeReduction { get; set; }
        private int SwingTrendLookback { get; set; }
        private int MinTrendSwings { get; set; }
        private int MomentumPeriod { get; set; }
        private int MinTrendConfirmations { get; set; }
        private int ExitOnSessionCloseTime { get; set; }

        // Serialization for brush colors
        [Browsable(false)]
        public string CAHOLDColorSerializable
        {
            get { return Serialize.BrushToString(CAHOLDColor); }
            set { CAHOLDColor = Serialize.StringToBrush(value); }
        }

        [Browsable(false)]
        public string CBLOHDColorSerializable
        {
            get { return Serialize.BrushToString(CBLOHDColor); }
            set { CBLOHDColor = Serialize.StringToBrush(value); }
        }

        [Browsable(false)]
        public string UpArrowColorSerializable
        {
            get { return Serialize.BrushToString(UpArrowColor); }
            set { UpArrowColor = Serialize.StringToBrush(value); }
        }

        [Browsable(false)]
        public string DownArrowColorSerializable
        {
            get { return Serialize.BrushToString(DownArrowColor); }
            set { DownArrowColor = Serialize.StringToBrush(value); }
        }
        #endregion
    }
}