# B2B Enhanced Strategy for NinjaTrader 8

## Overview

The B2B Enhanced strategy implements CAHOLD (Close Above High Of Low Day) and CBLOHD (Close Below Low Of High Day) pattern recognition for trend reversal trading. This is a completely refactored version with modular architecture, advanced risk management, and enhanced configurability.

**Version**: 6.0  
**Date**: January 1, 2025  
**Architecture**: Modular partial classes  

## Strategy Definition

**B2B** is an acronym for "Bearish to Bullish" and vice versa:

- **CAHOLD**: Close Above High Of Low Day - A bullish reversal pattern signaling potential trend change from bearish to bullish
- **CBLOHD**: Close Below Low Of High Day - A bearish reversal pattern signaling potential trend change from bullish to bearish

## Key Features

### 🎯 Pattern Recognition
- Advanced swing point detection with volume confirmation
- Multi-method trend analysis (swing analysis, moving averages, momentum)
- Configurable pattern filters and thresholds
- Support for both body-only and full candle analysis

### 💰 Risk Management
- Kelly Criterion position sizing with configurable fraction
- Consecutive loss reduction with exponential decay
- Daily loss and profit limits
- Dynamic stop loss validation and adjustment
- Comprehensive drawdown tracking

### 📊 Trade Management
- Multi-stage profit targets (1.5:1, 3:1, 5:1 risk/reward ratios)
- Progressive trailing stops that tighten as profits increase
- Intraday reversal detection and execution
- Position re-evaluation based on changing market conditions

### ⚙️ Advanced Configuration
- 50+ configurable parameters with Range validation
- Cached indicators for improved performance
- Time-based trading filters with timezone support
- Comprehensive visualization options

## File Structure

```
Strategy Files:
├── B2BEnhanced.cs          # Main strategy coordination
├── PatternDetector.cs      # Pattern recognition logic  
├── RiskManager.cs          # Risk management & position sizing
└── TradeManager.cs         # Trade execution & management
```

## Installation

1. Copy all four `.cs` files to your NinjaTrader 8 strategies folder:
   ```
   Documents\NinjaTrader 8\bin\Custom\Strategies\
   ```

2. Compile the strategy in NinjaTrader (Tools > Edit NinjaScript > Strategies > Compile)

3. The strategy will appear as "B2B Enhanced v6.0" in the strategy selection

## Configuration

The strategy includes 11 parameter groups:

1. **Version Info** - Version and date information
2. **Account/Risk** - Core risk parameters and Kelly Criterion settings  
3. **Position Management** - Position sizing and dynamic management
4. **ATR Settings** - Stop loss and target multipliers
5. **Pattern Detection** - Swing point and pattern recognition settings
6. **Time Filter** - Trading session time restrictions
7. **Visualization** - Chart display options
8. **Backtest Settings** - Historical testing configuration
9. **Pattern Thresholds** - Fine-tuning for pattern recognition
10. **Risk Configuration** - Advanced risk management parameters
11. **Target Configuration** - Profit target and trailing stop settings

## Default Settings

The strategy comes with thoroughly tested default parameters:
- **Risk Per Trade**: 1.0% of account
- **Kelly Fraction**: 0.5 (conservative half-Kelly)
- **ATR Period**: 14 bars
- **Stop Loss Multiplier**: 2.0x ATR (with 0.8 tightening factor)
- **Profit Targets**: 1.5:1, 3:1, 5:1 risk/reward ratios
- **Trading Hours**: 9:30 AM - 4:00 PM ET

## Performance Improvements

Compared to the original monolithic version:
- **63% reduction** in duplicated code through unified entry logic
- **Cached indicators** eliminate redundant calculations
- **Optimized algorithms** with early termination logic
- **Enhanced error handling** and comprehensive logging

## Documentation

- **REFACTORING_DOCUMENTATION.md** - Detailed technical documentation of the modular architecture
- **XML Comments** - Comprehensive inline documentation for all methods and properties

## Compatibility

- **NinjaTrader 8**: Full compatibility
- **Time Frames**: 1-minute to daily charts (auto-adjusts parameters)
- **Instruments**: Stocks, futures, forex (configurable point values)
- **Backtesting**: Full historical testing support with detailed trade logs

## Support

This strategy maintains 100% backward compatibility with the original B2B strategy while providing significant enhancements in maintainability, performance, and configurability.